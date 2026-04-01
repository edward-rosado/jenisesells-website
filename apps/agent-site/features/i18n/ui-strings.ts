import type { SupportedLocale } from "./locale-map";

export interface UiStrings {
  // Layout
  skipToContent: string;

  // Nav
  contactMe: string;
  openMenu: string;
  closeMenu: string;

  // Footer
  cellLabel: string;
  privacyPolicy: string;
  termsOfUse: string;
  accessibility: string;
  allRightsReserved: string;
  licensedSalesperson: string;
  disclaimerTemplate: string;
  servingTemplate: string;

  // CMA Section
  formAriaLabel: string;
  submissionError: string;

  // Lead Form labels
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
  submitSellingLabel: string;
  submitBuyingLabel: string;
  submitDefaultLabel: string;

  // Thank you page
  thankYouFallbackHeading: string;
  thankYouFallbackSubheading: string;
}

const en: UiStrings = {
  skipToContent: "Skip to main content",
  contactMe: "Contact Me",
  openMenu: "Open menu",
  closeMenu: "Close menu",
  cellLabel: "Cell:",
  privacyPolicy: "Privacy Policy",
  termsOfUse: "Terms of Use",
  accessibility: "Accessibility",
  allRightsReserved: "All rights reserved.",
  licensedSalesperson: "Licensed Real Estate Salesperson",
  disclaimerTemplate: "The information on this website is for general informational purposes only.",
  servingTemplate: "Serving",
  formAriaLabel: "Home Value Request Form",
  submissionError: "Something went wrong. Please try again.",
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
  submitSellingLabel: "Get My Free Home Value Report \u2192",
  submitBuyingLabel: "Find My Dream Home \u2192",
  submitDefaultLabel: "Get Started \u2192",
  thankYouFallbackHeading: "Thank You!",
  thankYouFallbackSubheading: "Your Free Home Value Report Is Being Prepared Now!",
};

const es: UiStrings = {
  skipToContent: "Ir al contenido principal",
  contactMe: "Cont\u00e1ctame",
  openMenu: "Abrir men\u00fa",
  closeMenu: "Cerrar men\u00fa",
  cellLabel: "Cel:",
  privacyPolicy: "Pol\u00edtica de Privacidad",
  termsOfUse: "T\u00e9rminos de Uso",
  accessibility: "Accesibilidad",
  allRightsReserved: "Todos los derechos reservados.",
  licensedSalesperson: "Agente de Bienes Ra\u00edces Licenciado/a",
  disclaimerTemplate: "La informaci\u00f3n en este sitio web es solo para fines informativos generales.",
  servingTemplate: "Sirviendo",
  formAriaLabel: "Formulario de Solicitud de Valor de Vivienda",
  submissionError: "Algo sali\u00f3 mal. Por favor, int\u00e9ntalo de nuevo.",
  imBuying: "Quiero Comprar",
  imSelling: "Quiero Vender",
  firstName: "Nombre",
  lastName: "Apellido",
  email: "Correo Electr\u00f3nico",
  phone: "Tel\u00e9fono",
  desiredArea: "\u00c1rea Deseada",
  selectArea: "Seleccionar \u00e1rea...",
  minPrice: "Precio M\u00ednimo",
  maxPrice: "Precio M\u00e1ximo",
  minBeds: "Habitaciones M\u00edn.",
  minBaths: "Ba\u00f1os M\u00edn.",
  preApproved: "\u00bfPre-aprobado/a?",
  preApprovalAmount: "Monto de Pre-aprobaci\u00f3n",
  propertyAddress: "Direcci\u00f3n de la Propiedad",
  city: "Ciudad",
  state: "Estado",
  zip: "C\u00f3digo Postal",
  beds: "Habitaciones",
  baths: "Ba\u00f1os",
  sqft: "Pies\u00b2",
  timelineLabel: "\u00bfCu\u00e1ndo planea",
  timelineAsap: "Lo antes posible",
  timeline1to3: "1-3 Meses",
  timeline3to6: "3-6 Meses",
  timeline6to12: "6-12 Meses",
  timelineJustCurious: "Solo Curiosidad",
  select: "Seleccionar...",
  notesSellingOnly: "Cu\u00e9ntenos sobre la propiedad",
  notesBuyingOnly: "Describa su hogar ideal",
  notesBoth: "Cu\u00e9ntenos sobre su propiedad y lo que busca",
  notesDefault: "Notas Adicionales",
  notesPlaceholderSelling: "Renovaciones recientes, caracter\u00edsticas \u00fanicas, cronograma, expectativas de precio...",
  notesPlaceholderBuying: "Requisitos, preferencias de vecindario, distritos escolares, flexibilidad de presupuesto...",
  notesPlaceholderBoth: "Describa su propiedad en venta y lo que busca en su pr\u00f3ximo hogar...",
  optional: "opcional",
  addressPoweredBy: "Autocompletado de direcci\u00f3n por Google Maps.",
  cmaDisclaimer:
    "Este An\u00e1lisis Comparativo de Mercado no es una tasaci\u00f3n. Es una estimaci\u00f3n del valor " +
    "de mercado basada en datos de ventas comparables y condiciones del mercado. No debe usarse " +
    "en lugar de una tasaci\u00f3n para fines de pr\u00e9stamo. Solo un tasador licenciado puede proporcionar una tasaci\u00f3n.",
  validationSelectMode: "Por favor, seleccione al menos uno: comprar o vender.",
  validationSelectTimeline: "Por favor, seleccione un plazo.",
  validationConsent: "Debe dar su consentimiento para recibir comunicaciones antes de enviar.",
  submitSellingLabel: "Obtener Mi Informe de Valor Gratis \u2192",
  submitBuyingLabel: "Encontrar Mi Hogar Ideal \u2192",
  submitDefaultLabel: "Comenzar \u2192",
  thankYouFallbackHeading: "\u00a1Gracias!",
  thankYouFallbackSubheading: "\u00a1Su Informe de Valor de Vivienda Se Est\u00e1 Preparando!",
};

const strings: Record<string, UiStrings> = { en, es };

/** Get UI strings for a locale, falling back to English */
export function getUiStrings(locale?: string): UiStrings {
  return strings[locale ?? "en"] ?? en;
}
