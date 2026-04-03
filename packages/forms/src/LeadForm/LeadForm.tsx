"use client";

import { useState, useCallback, type FormEvent } from "react";
import type {
  LeadFormData,
  LeadType,
  BuyerDetails,
  SellerDetails,
  PreApprovalStatus,
  Timeline,
} from "@real-estate-star/domain";
import { useGooglePlacesAutocomplete } from "./useGooglePlacesAutocomplete";
import { AddressAutocomplete } from "./AddressAutocomplete";

const TCPA_CONSENT_TEXT = (agentName: string) =>
  `By submitting this form, you consent to receive email communications from ${agentName} ` +
  `regarding your real estate inquiry, including market updates and property information. ` +
  `You also consent to be contacted by ${agentName} by phone at the number provided. ` +
  `You may unsubscribe from emails at any time. Consent is not a condition of purchasing ` +
  `any property or service.`;

/** Translatable labels for all visible text in the LeadForm. All fields optional — English defaults used when omitted. */
export interface LeadFormLabels {
  imBuying: string;
  imSelling: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  desiredArea: string;
  selectArea: string;
  minPrice: string;
  maxPrice: string;
  minBeds: string;
  minBaths: string;
  preApproved: string;
  preApprovalAmount: string;
  propertyAddress: string;
  city: string;
  state: string;
  zip: string;
  beds: string;
  baths: string;
  sqft: string;
  timelineLabel: string;
  timelineAsap: string;
  timeline1to3: string;
  timeline3to6: string;
  timeline6to12: string;
  timelineJustCurious: string;
  select: string;
  notesSellingOnly: string;
  notesBuyingOnly: string;
  notesBoth: string;
  notesDefault: string;
  notesPlaceholderSelling: string;
  notesPlaceholderBuying: string;
  notesPlaceholderBoth: string;
  optional: string;
  addressPoweredBy: string;
  cmaDisclaimer: string;
  validationSelectMode: string;
  validationSelectTimeline: string;
  validationConsent: string;
}

const DEFAULT_LABELS: LeadFormLabels = {
  imBuying: "I'm Buying",
  imSelling: "I'm Selling",
  firstName: "First Name",
  lastName: "Last Name",
  email: "Email",
  phone: "Phone",
  desiredArea: "Desired Area",
  selectArea: "Select area...",
  minPrice: "Min Price",
  maxPrice: "Max Price",
  minBeds: "Min Beds",
  minBaths: "Min Baths",
  preApproved: "Pre-Approved?",
  preApprovalAmount: "Pre-Approval Amount",
  propertyAddress: "Property Address",
  city: "City",
  state: "State",
  zip: "Zip",
  beds: "Beds",
  baths: "Baths",
  sqft: "Sqft",
  timelineLabel: "When are you looking to",
  timelineAsap: "ASAP",
  timeline1to3: "1-3 Months",
  timeline3to6: "3-6 Months",
  timeline6to12: "6-12 Months",
  timelineJustCurious: "Just Curious",
  select: "Select...",
  notesSellingOnly: "Tell us about the property",
  notesBuyingOnly: "Describe your dream home",
  notesBoth: "Tell us about your property and what you're looking for",
  notesDefault: "Additional Notes",
  notesPlaceholderSelling: "Recent renovations, unique features, timeline, price expectations...",
  notesPlaceholderBuying: "Must-haves, neighborhood preferences, school districts, budget flexibility...",
  notesPlaceholderBoth: "Describe your property for sale and what you're looking for in your next home...",
  optional: "optional",
  addressPoweredBy: "Address autocomplete powered by Google Maps.",
  cmaDisclaimer:
    "This Comparative Market Analysis is not an appraisal. It is an estimate of market " +
    "value based on comparable sales data and market conditions. It should not be used in " +
    "lieu of an appraisal for lending purposes. Only a licensed appraiser can provide an appraisal.",
  validationSelectMode: "Please select at least one: buying or selling.",
  validationSelectTimeline: "Please select a timeline.",
  validationConsent: "You must consent to receive communications before submitting.",
};

export interface LeadFormProps {
  defaultState: string;
  googleMapsApiKey?: string;
  onSubmit: (data: LeadFormData) => void | Promise<void>;
  initialMode?: LeadType[];
  submitLabel?: string | ((isBuying: boolean, isSelling: boolean) => string);
  disabled?: boolean;
  error?: string;
  serviceAreas?: string[];
  showCmaDisclaimer?: boolean;
  agentFirstName?: string;
  /** Cloudflare Turnstile token — submit is disabled until provided. Omit to skip Turnstile gating. */
  turnstileToken?: string | null;
  /** Render slot for a Turnstile widget (or any CAPTCHA). Rendered above the submit button. */
  captchaSlot?: React.ReactNode;
  /** Translated labels for form fields. TCPA consent stays in English (legally required). */
  labels?: Partial<LeadFormLabels>;
  /** BCP 47 locale code (e.g., "en", "es") — forwarded to the API with the submission. */
  locale?: string;
}

function parseOptionalNumber(value: string): number | undefined {
  if (!value) return undefined;
  const n = Number(value);
  /* v8 ignore next -- defensive guard: number inputs prevent non-numeric values in browsers */
  return Number.isNaN(n) ? undefined : n;
}

type FormFields = {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  desiredArea: string;
  minPrice: string;
  maxPrice: string;
  minBeds: string;
  minBaths: string;
  preApproved: string;
  preApprovalAmount: string;
  address: string;
  city: string;
  sellerState: string;
  zip: string;
  beds: string;
  baths: string;
  sqft: string;
  timeline: string;
  notes: string;
};

export function LeadForm({
  defaultState,
  googleMapsApiKey = "",
  onSubmit,
  initialMode = [],
  submitLabel: submitLabelProp,
  disabled = false,
  error,
  serviceAreas = [],
  showCmaDisclaimer = false,
  agentFirstName,
  turnstileToken,
  captchaSlot,
  labels: labelsProp,
  locale,
}: LeadFormProps) {
  const L = { ...DEFAULT_LABELS, ...labelsProp };
  const [isBuying, setIsBuying] = useState(initialMode.includes("buying"));
  const [isSelling, setIsSelling] = useState(initialMode.includes("selling"));
  const submitLabel: string | ((b: boolean, s: boolean) => string) = submitLabelProp
    ?? (agentFirstName
      ? (_b: boolean, s: boolean) => s ? "Get My Free CMA" : `Connect with ${agentFirstName}`
      : "Get Started");
  const [submitting, setSubmitting] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [tcpaConsent, setTcpaConsent] = useState(false);
  const [honeypot, setHoneypot] = useState("");
  const [fields, setFields] = useState<FormFields>({
    firstName: "",
    lastName: "",
    email: "",
    phone: "",
    desiredArea: "",
    minPrice: "",
    maxPrice: "",
    minBeds: "",
    minBaths: "",
    preApproved: "",
    preApprovalAmount: "",
    address: "",
    city: "",
    sellerState: defaultState,
    zip: "",
    beds: "",
    baths: "",
    sqft: "",
    timeline: "",
    notes: "",
  });

  const updateField = useCallback(
    (name: keyof FormFields, value: string) => {
      setFields((prev) => ({ ...prev, [name]: value }));
    },
    [],
  );

  const autocomplete = useGooglePlacesAutocomplete({
    apiKey: googleMapsApiKey,
    stateCode: defaultState,
    /* v8 ignore start -- covered by useGooglePlacesAutocomplete.test.ts integration */
    onPlaceSelected: useCallback(
      (place: { address: string; city: string; state: string; zip: string }) => {
        if (place.state && place.state !== defaultState) {
          setValidationError(
            `Sorry, we only serve ${defaultState}. The address you selected is in ${place.state}.`,
          );
          setFields((prev) => ({
            ...prev,
            address: "",
            city: "",
            sellerState: defaultState,
            zip: "",
          }));
          autocomplete.setQuery("");
          return;
        }
        setValidationError(null);
        setFields((prev) => ({
          ...prev,
          address: place.address,
          city: place.city,
          sellerState: place.state || defaultState,
          zip: place.zip,
        }));
      },
      [defaultState],
    ),
    /* v8 ignore stop */
    enabled: isSelling,
  });

  function getTimelineLabel(): string {
    if (isBuying && isSelling) return "buy/sell";
    if (isBuying) return "buy";
    return "sell";
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();

    if (!isBuying && !isSelling) {
      setValidationError(L.validationSelectMode);
      return;
    }

    /* v8 ignore next -- button is disabled when submitting; belt-and-suspenders guard */
    if (submitting) return;

    if (!fields.timeline) {
      setValidationError(L.validationSelectTimeline);
      return;
    }

    if (!tcpaConsent) {
      setValidationError(L.validationConsent);
      return;
    }

    // SECURITY: Honeypot — bots fill all fields, real users never see this
    if (honeypot) {
      // Fake success to avoid revealing detection to the bot
      setSubmitting(true);
      return;
    }

    if (isSelling && fields.sellerState && fields.sellerState !== defaultState) {
      setValidationError(
        `Sorry, we only serve ${defaultState}. The address you entered is in ${fields.sellerState}.`,
      );
      return;
    }

    setValidationError(null);

    const leadTypes: LeadType[] = [];
    if (isBuying) leadTypes.push("buying");
    if (isSelling) leadTypes.push("selling");

    let buyer: BuyerDetails | undefined;
    if (isBuying) {
      buyer = {
        desiredArea: fields.desiredArea,
        minPrice: parseOptionalNumber(fields.minPrice),
        maxPrice: parseOptionalNumber(fields.maxPrice),
        minBeds: parseOptionalNumber(fields.minBeds),
        minBaths: parseOptionalNumber(fields.minBaths),
        ...( fields.preApproved && { preApproved: fields.preApproved as PreApprovalStatus }),
        preApprovalAmount: parseOptionalNumber(fields.preApprovalAmount),
      };
    }

    let seller: SellerDetails | undefined;
    if (isSelling) {
      seller = {
        address: fields.address,
        city: fields.city,
        state: fields.sellerState,
        zip: fields.zip,
        beds: parseOptionalNumber(fields.beds),
        baths: parseOptionalNumber(fields.baths),
        sqft: parseOptionalNumber(fields.sqft),
      };
    }

    const data: LeadFormData = {
      leadTypes,
      firstName: fields.firstName,
      lastName: fields.lastName,
      email: fields.email,
      phone: fields.phone,
      buyer,
      seller,
      timeline: fields.timeline as Timeline,
      notes: fields.notes || undefined,
      marketingConsent: {
        optedIn: tcpaConsent,
        consentText: TCPA_CONSENT_TEXT(agentFirstName ?? "the agent"),
        channels: ["email", "calls"],
      },
      locale,
    };

    setSubmitting(true);
    try {
      await onSubmit(data);
    } catch {
      // error handled by consumer
    } finally {
      setSubmitting(false);
    }
  }

  const inputStyle: React.CSSProperties = {
    width: "100%",
    border: "2px solid #e0e0e0",
    borderRadius: 8,
    padding: "12px 16px",
    fontSize: 15,
    boxSizing: "border-box",
    outline: "none",
  };

  const labelStyle: React.CSSProperties = {
    display: "block",
    fontWeight: 600,
    marginBottom: 4,
    fontSize: 14,
  };

  const fieldGroupStyle: React.CSSProperties = {
    marginBottom: 16,
  };

  const pillBase: React.CSSProperties = {
    flex: 1,
    borderRadius: 25,
    padding: "10px 22px",
    fontWeight: 600,
    fontSize: 15,
    cursor: "pointer",
    borderWidth: 2,
    borderStyle: "solid",
    borderColor: "#e0e0e0",
    background: "#fff",
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    transition: "all 200ms ease",
  };

  const pillChecked: React.CSSProperties = {
    ...pillBase,
    background: "var(--color-primary, #1B5E20)",
    color: "#fff",
    borderColor: "var(--color-primary, #1B5E20)",
  };

  const requiredMark = <span style={{ color: "red", marginLeft: 2 }} aria-hidden="true">*</span>;

  function field(
    name: keyof FormFields,
    props?: {
      id?: string;
      type?: string;
      ref?: React.Ref<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>;
      required?: boolean;
    },
  ) {
    return {
      id: props?.id ?? `lf-${name}`,
      type: props?.type,
      ref: props?.ref as React.Ref<any>,
      style: inputStyle,
      value: fields[name],
      required: props?.required,
      "aria-required": props?.required || undefined,
      onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
        updateField(name, e.target.value),
    };
  }

  return (
    <form
      onSubmit={handleSubmit}
      aria-describedby="lf-error"
      style={{
        background: "#fff",
        borderRadius: 14,
        padding: "clamp(20px, 5vw, 40px)",
        boxShadow: "0 4px 24px rgba(0,0,0,0.08)",
        maxWidth: 600,
        margin: "0 auto",
      }}
    >
      {/* SECURITY: Honeypot field — hidden from real users, catches bots that fill all fields */}
      <input
        type="text"
        name="website"
        aria-hidden="true"
        tabIndex={-1}
        autoComplete="off"
        value={honeypot}
        onChange={(e) => setHoneypot(e.target.value)}
        style={{ position: "absolute", left: -9999, width: 1, height: 1, overflow: "hidden" }}
      />

      {/* SECURITY: Static CSS only. Never interpolate dynamic values here. */}
      <style>{`
        .res-lead-form-row { display: flex; gap: 16px; }
        .res-lead-form-row > div { flex: 1; }
        @media (max-width: 600px) {
          .res-lead-form-row { flex-direction: column; gap: 0; }
        }
        .res-lead-form-pill:hover {
          transform: translateY(-1px);
          box-shadow: 0 4px 12px rgba(0,0,0,0.15);
          filter: brightness(1.05);
        }
        .res-lead-form-pill:active {
          transform: translateY(0);
          box-shadow: 0 1px 4px rgba(0,0,0,0.1);
        }
        .res-lead-form-pill:focus-visible {
          outline: 2px solid var(--color-primary, #1B5E20);
          outline-offset: 2px;
        }
        .res-lead-form-submit:hover:not(:disabled) {
          transform: translateY(-2px);
          box-shadow: 0 8px 25px rgba(0,0,0,0.3);
          filter: brightness(1.1);
        }
        .res-lead-form-submit:active:not(:disabled) {
          transform: translateY(1px);
          box-shadow: 0 2px 8px rgba(0,0,0,0.2);
          filter: brightness(0.95);
        }
      `}</style>

      {/* Mode pills */}
      <div style={{ marginBottom: 20, display: "flex", gap: 12 }}>
        <button
          type="button"
          role="checkbox"
          aria-checked={isBuying}
          className="res-lead-form-pill"
          onClick={() => { setIsBuying((v) => !v); setValidationError(null); }}
          style={{
            ...(isBuying ? pillChecked : pillBase),
            ...(validationError && !isBuying && !isSelling ? { borderColor: "red" } : {}),
          }}
        >
          {L.imBuying}
        </button>
        <button
          type="button"
          role="checkbox"
          aria-checked={isSelling}
          className="res-lead-form-pill"
          onClick={() => { setIsSelling((v) => !v); setValidationError(null); }}
          style={{
            ...(isSelling ? pillChecked : pillBase),
            ...(validationError && !isBuying && !isSelling ? { borderColor: "red" } : {}),
          }}
        >
          {L.imSelling}
        </button>
      </div>

      {/* Contact fields */}
      <div className="res-lead-form-row">
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-firstName">{L.firstName}{requiredMark}</label>
          <input {...field("firstName", { required: true })} />
        </div>
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-lastName">{L.lastName}{requiredMark}</label>
          <input {...field("lastName", { required: true })} />
        </div>
      </div>
      <div className="res-lead-form-row">
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-email">{L.email}{requiredMark}</label>
          <input {...field("email", { type: "email", required: true })} />
        </div>
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-phone">{L.phone}{requiredMark}</label>
          <input {...field("phone", { type: "tel", required: true })} />
        </div>
      </div>

      {/* Buyer card */}
      <div
        data-testid="buyer-card"
        style={{
          background: "#FFFDE7",
          border: isBuying ? "1px solid #F0E68C" : "none",
          borderRadius: 10,
          padding: isBuying ? 20 : 0,
          marginBottom: isBuying ? 16 : 0,
          maxHeight: isBuying ? "800px" : "0",
          opacity: isBuying ? 1 : 0,
          overflow: "hidden",
          transition: "all 300ms ease",
        }}
      >
        {isBuying && (
          <>
            <div style={fieldGroupStyle}>
              <label style={labelStyle} htmlFor="lf-desiredArea">{L.desiredArea}{requiredMark}</label>
              {serviceAreas.length > 0 ? (
                <select {...field("desiredArea", { required: true })}>
                  <option value="">{L.selectArea}</option>
                  {serviceAreas.map((area) => (
                    <option key={area} value={area}>{area}</option>
                  ))}
                </select>
              ) : (
                <input {...field("desiredArea", { required: true })} />
              )}
            </div>
            <div className="res-lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-minPrice">{L.minPrice}</label>
                <input {...field("minPrice", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-maxPrice">{L.maxPrice}</label>
                <input {...field("maxPrice", { type: "number" })} />
              </div>
            </div>
            <div className="res-lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-minBeds">{L.minBeds}</label>
                <input {...field("minBeds", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-minBaths">{L.minBaths}</label>
                <input {...field("minBaths", { type: "number" })} />
              </div>
            </div>
            <div className="res-lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-preApproved">{L.preApproved}</label>
                <select {...field("preApproved")}>
                  <option value="">{L.select}</option>
                  <option value="yes">Yes</option>
                  <option value="no">No</option>
                  <option value="in-progress">In Progress</option>
                </select>
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-preApprovalAmount">{L.preApprovalAmount}</label>
                <input {...field("preApprovalAmount", { type: "number" })} />
              </div>
            </div>
          </>
        )}
      </div>

      {/* Seller card */}
      <div
        data-testid="seller-card"
        style={{
          background: "#E8F5E9",
          border: isSelling ? "1px solid #A5D6A7" : "none",
          borderRadius: 10,
          padding: isSelling ? 20 : 0,
          marginBottom: isSelling ? 16 : 0,
          maxHeight: isSelling ? "800px" : "0",
          opacity: isSelling ? 1 : 0,
          overflow: isSelling ? "visible" : "hidden",
          transition: "all 300ms ease",
        }}
      >
        {isSelling && (
          <>
            <div style={fieldGroupStyle}>
              <label style={labelStyle} htmlFor="lf-address">{L.propertyAddress}{requiredMark}</label>
              <AddressAutocomplete
                query={autocomplete.query || fields.address}
                setQuery={(value) => {
                  autocomplete.setQuery(value);
                  updateField("address", value);
                }}
                suggestions={autocomplete.suggestions}
                highlightedIndex={autocomplete.highlightedIndex}
                setHighlightedIndex={autocomplete.setHighlightedIndex}
                selectSuggestion={autocomplete.selectSuggestion}
                clearSuggestions={autocomplete.clearSuggestions}
                fetchError={autocomplete.fetchError}
                inputStyle={inputStyle}
                required
                id="lf-address"
              />
            </div>
            <div className="res-lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-city">{L.city}{requiredMark}</label>
                <input {...field("city", { required: true })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-sellerState">{L.state}{requiredMark}</label>
                <input {...field("sellerState", { id: "lf-sellerState", required: true })} readOnly aria-readonly="true" style={{ ...inputStyle, backgroundColor: "#f5f5f5", cursor: "default" }} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-zip">{L.zip}{requiredMark}</label>
                <input {...field("zip", { required: true })} />
              </div>
            </div>
            <div className="res-lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-beds">{L.beds}</label>
                <input {...field("beds", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-baths">{L.baths}</label>
                <input {...field("baths", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-sqft">{L.sqft}</label>
                <input {...field("sqft", { type: "number" })} />
              </div>
            </div>
            <p style={{ fontSize: 11, color: "#595959", marginTop: 4, marginBottom: 0 }}>
              {L.addressPoweredBy}
            </p>
          </>
        )}
      </div>

      {/* Timeline */}
      {(isBuying || isSelling) && (
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-timeline">
            {L.timelineLabel} {getTimelineLabel()}?{requiredMark}
          </label>
          <select {...field("timeline", { required: true })}>
            <option value="">{L.select}</option>
            <option value="asap">{L.timelineAsap}</option>
            <option value="1-3months">{L.timeline1to3}</option>
            <option value="3-6months">{L.timeline3to6}</option>
            <option value="6-12months">{L.timeline6to12}</option>
            <option value="justcurious">{L.timelineJustCurious}</option>
          </select>
        </div>
      )}

      {/* Notes */}
      {(() => {
        const notesLabel = isSelling && !isBuying
          ? L.notesSellingOnly
          : !isSelling && isBuying
          ? L.notesBuyingOnly
          : isSelling && isBuying
          ? L.notesBoth
          : L.notesDefault;
        const notesPlaceholder = isSelling && !isBuying
          ? L.notesPlaceholderSelling
          : !isSelling && isBuying
          ? L.notesPlaceholderBuying
          : isSelling && isBuying
          ? L.notesPlaceholderBoth
          : undefined;
        return (
          <div style={fieldGroupStyle}>
            <label style={labelStyle} htmlFor="lf-notes">{notesLabel} ({L.optional})</label>
            <textarea
              {...field("notes")}
              placeholder={notesPlaceholder}
              style={{ ...inputStyle, minHeight: 80, resize: "vertical" }}
            />
          </div>
        );
      })()}

      {/* Errors */}
      <div id="lf-error" aria-live="polite" role="alert">
        {validationError && (
          <p style={{ color: "#d32f2f", fontSize: 14, marginBottom: 12 }}>{validationError}</p>
        )}
        {error && (
          <p style={{ color: "#d32f2f", fontSize: 14, marginBottom: 12 }}>{error}</p>
        )}
      </div>

      {/* TCPA Consent */}
      <label style={{ display: "flex", alignItems: "flex-start", gap: "8px", fontSize: 11, color: "#595959", textAlign: "left", marginTop: "16px" }}>
        <input
          type="checkbox"
          data-testid="tcpa-consent"
          checked={tcpaConsent}
          onChange={(e) => setTcpaConsent(e.target.checked)}
          style={{ marginTop: "2px", flexShrink: 0 }}
        />
        <span>{TCPA_CONSENT_TEXT(agentFirstName ?? "the agent")}</span>
      </label>

      {/* CAPTCHA slot — Turnstile widget or other challenge rendered by parent */}
      {captchaSlot}

      {/* Submit */}
      <div style={{ marginTop: 12 }} />
      <button
        type="submit"
        className="res-lead-form-submit"
        disabled={disabled || submitting || (turnstileToken !== undefined && !turnstileToken)}
        style={{
          width: "100%",
          padding: "14px 32px",
          borderRadius: 30,
          border: "none",
          background: "var(--color-accent, #A68A3E)",
          color: "#fff",
          fontSize: 17,
          fontWeight: 700,
          cursor: disabled || submitting ? "not-allowed" : "pointer",
          transition: "all 200ms ease",
          opacity: disabled || submitting ? 0.6 : 1,
        }}
      >
        {typeof submitLabel === "function" ? submitLabel(isBuying, isSelling) : submitLabel}
      </button>

      {showCmaDisclaimer && (
        <span
          style={{
            display: "block",
            fontSize: 11,
            color: "#595959",
            marginTop: 16,
            textAlign: "center",
            lineHeight: 1.5,
          }}
        >
          {L.cmaDisclaimer}
        </span>
      )}
    </form>
  );
}
