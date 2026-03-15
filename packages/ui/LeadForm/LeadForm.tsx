"use client";

import { useState, useRef, useCallback, type FormEvent } from "react";
import type {
  LeadFormData,
  LeadType,
  BuyerDetails,
  SellerDetails,
  PreApprovalStatus,
  Timeline,
} from "@real-estate-star/shared-types";
import { useGoogleMapsAutocomplete } from "./useGoogleMapsAutocomplete";

export interface LeadFormProps {
  defaultState: string;
  googleMapsApiKey: string;
  onSubmit: (data: LeadFormData) => void | Promise<void>;
  initialMode?: LeadType[];
  submitLabel?: string;
  disabled?: boolean;
  error?: string;
}

function parseOptionalNumber(value: string): number | undefined {
  if (!value) return undefined;
  return Number(value);
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
  googleMapsApiKey,
  onSubmit,
  initialMode = [],
  submitLabel = "Get Started",
  disabled = false,
  error,
}: LeadFormProps) {
  const [isBuying, setIsBuying] = useState(initialMode.includes("buying"));
  const [isSelling, setIsSelling] = useState(initialMode.includes("selling"));
  const [submitting, setSubmitting] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
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

  const addressRef = useRef<HTMLInputElement>(null);

  const updateField = useCallback(
    (name: keyof FormFields, value: string) => {
      setFields((prev) => ({ ...prev, [name]: value }));
    },
    [],
  );

  useGoogleMapsAutocomplete({
    apiKey: googleMapsApiKey,
    inputRef: addressRef,
    /* v8 ignore start -- covered by useGoogleMapsAutocomplete.test.ts integration */
    onPlaceSelected: useCallback(
      (place: { address: string; city: string; state: string; zip: string }) => {
        setFields((prev) => ({
          ...prev,
          address: place.address,
          city: place.city,
          sellerState: place.state,
          zip: place.zip,
        }));
      },
      [],
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
      setValidationError("Please select at least one: buying or selling.");
      return;
    }

    /* v8 ignore next -- button is disabled when submitting; belt-and-suspenders guard */
    if (submitting) return;

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
    borderRadius: 25,
    padding: "10px 22px",
    fontWeight: 600,
    fontSize: 15,
    cursor: "pointer",
    border: "2px solid #e0e0e0",
    background: "#fff",
    display: "inline-flex",
    alignItems: "center",
    gap: 8,
    marginRight: 12,
    transition: "all 200ms ease",
  };

  const pillChecked: React.CSSProperties = {
    ...pillBase,
    background: "var(--color-primary, #1B5E20)",
    color: "#fff",
    borderColor: "var(--color-primary, #1B5E20)",
  };

  function field(
    name: keyof FormFields,
    props?: {
      id?: string;
      type?: string;
      ref?: React.Ref<HTMLInputElement>;
    },
  ) {
    return {
      id: props?.id ?? `lf-${name}`,
      type: props?.type,
      ref: props?.ref,
      style: inputStyle,
      value: fields[name],
      onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
        updateField(name, e.target.value),
    };
  }

  return (
    <form
      onSubmit={handleSubmit}
      style={{
        background: "#fff",
        borderRadius: 14,
        padding: "clamp(20px, 5vw, 40px)",
        boxShadow: "0 4px 24px rgba(0,0,0,0.08)",
        maxWidth: 600,
        margin: "0 auto",
      }}
    >
      <style>{`
        .lead-form-row { display: flex; gap: 16px; }
        .lead-form-row > div { flex: 1; }
        @media (max-width: 600px) {
          .lead-form-row { flex-direction: column; gap: 0; }
        }
      `}</style>

      {/* Mode pills */}
      <div style={{ marginBottom: 20, display: "flex", flexWrap: "wrap", gap: 8 }}>
        <span style={{ position: "relative", ...(isBuying ? pillChecked : pillBase) }}>
          <input
            id="lf-buying"
            type="checkbox"
            checked={isBuying}
            onChange={() => setIsBuying((v) => !v)}
            style={{ position: "absolute", opacity: 0, width: 0, height: 0, pointerEvents: "none" }}
          />
          <label htmlFor="lf-buying" style={{ cursor: "pointer", margin: 0 }}>I&apos;m Buying</label>
        </span>
        <span style={{ position: "relative", ...(isSelling ? pillChecked : pillBase) }}>
          <input
            id="lf-selling"
            type="checkbox"
            checked={isSelling}
            onChange={() => setIsSelling((v) => !v)}
            style={{ position: "absolute", opacity: 0, width: 0, height: 0, pointerEvents: "none" }}
          />
          <label htmlFor="lf-selling" style={{ cursor: "pointer", margin: 0 }}>I&apos;m Selling</label>
        </span>
      </div>

      {/* Contact fields */}
      <div className="lead-form-row">
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-firstName">First Name</label>
          <input {...field("firstName")} />
        </div>
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-lastName">Last Name</label>
          <input {...field("lastName")} />
        </div>
      </div>
      <div className="lead-form-row">
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-email">Email</label>
          <input {...field("email", { type: "email" })} />
        </div>
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-phone">Phone</label>
          <input {...field("phone", { type: "tel" })} />
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
              <label style={labelStyle} htmlFor="lf-desiredArea">Desired Area</label>
              <input {...field("desiredArea")} />
            </div>
            <div className="lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-minPrice">Min Price</label>
                <input {...field("minPrice", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-maxPrice">Max Price</label>
                <input {...field("maxPrice", { type: "number" })} />
              </div>
            </div>
            <div className="lead-form-row">
              <div style={fieldGroupStyle}>
                {(!isSelling || !fields.minBeds) && <label style={labelStyle} htmlFor="lf-minBeds">Min Beds</label>}
                <input {...field("minBeds", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                {(!isSelling || !fields.minBaths) && <label style={labelStyle} htmlFor="lf-minBaths">Min Baths</label>}
                <input {...field("minBaths", { type: "number" })} />
              </div>
            </div>
            <div className="lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-preApproved">Pre-Approved?</label>
                <select {...field("preApproved")}>
                  <option value="">Select...</option>
                  <option value="yes">Yes</option>
                  <option value="no">No</option>
                  <option value="in-progress">In Progress</option>
                </select>
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-preApprovalAmount">Pre-Approval Amount</label>
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
          overflow: "hidden",
          transition: "all 300ms ease",
        }}
      >
        {isSelling && (
          <>
            <div style={fieldGroupStyle}>
              <label style={labelStyle} htmlFor="lf-address">Property Address</label>
              <input {...field("address", { ref: addressRef })} />
            </div>
            <div className="lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-city">City</label>
                <input {...field("city")} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-sellerState">State</label>
                <input {...field("sellerState", { id: "lf-sellerState" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-zip">Zip</label>
                <input {...field("zip")} />
              </div>
            </div>
            <div className="lead-form-row">
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-beds">Beds</label>
                <input {...field("beds", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-baths">Baths</label>
                <input {...field("baths", { type: "number" })} />
              </div>
              <div style={fieldGroupStyle}>
                <label style={labelStyle} htmlFor="lf-sqft">Sqft</label>
                <input {...field("sqft", { type: "number" })} />
              </div>
            </div>
          </>
        )}
      </div>

      {/* Timeline */}
      {(isBuying || isSelling) && (
        <div style={fieldGroupStyle}>
          <label style={labelStyle} htmlFor="lf-timeline">
            When are you looking to {getTimelineLabel()}?
          </label>
          <select {...field("timeline")}>
            <option value="">Select...</option>
            <option value="asap">ASAP</option>
            <option value="1-3months">1-3 Months</option>
            <option value="3-6months">3-6 Months</option>
            <option value="6-12months">6-12 Months</option>
            <option value="justcurious">Just Curious</option>
          </select>
        </div>
      )}

      {/* Notes */}
      <div style={fieldGroupStyle}>
        <label style={labelStyle} htmlFor="lf-notes">Notes (optional)</label>
        <textarea {...field("notes")} style={{ ...inputStyle, minHeight: 80, resize: "vertical" }} />
      </div>

      {/* Validation error */}
      {validationError && (
        <p style={{ color: "red", fontSize: 14, marginBottom: 12 }}>{validationError}</p>
      )}

      {/* External error */}
      {error && (
        <p style={{ color: "red", fontSize: 14, marginBottom: 12 }}>{error}</p>
      )}

      {/* Submit */}
      <button
        type="submit"
        disabled={disabled || submitting}
        style={{
          width: "100%",
          padding: "14px 32px",
          borderRadius: 30,
          border: "none",
          background: "var(--color-accent, #C8A951)",
          color: "#fff",
          fontSize: 17,
          fontWeight: 700,
          cursor: disabled || submitting ? "not-allowed" : "pointer",
          transition: "all 200ms ease",
          opacity: disabled || submitting ? 0.6 : 1,
        }}
      >
        {submitLabel}
      </button>
    </form>
  );
}
