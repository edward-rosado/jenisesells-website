import { uiStringsEs } from "./ui-strings-es";

export interface UiStrings {
  // Skip nav
  skipToMainContent: string;

  // Nav
  contactMe: string;

  // Footer
  cellLabel: string;
  privacyPolicy: string;
  termsOfUse: string;
  accessibility: string;
  allRightsReserved: string;
  licensedRealEstateSalesperson: string;

  // CmaSection
  getMyFreeHomeValueReport: string;
  findMyDreamHome: string;
  getStarted: string;
  somethingWentWrong: string;

  // LeadForm labels
  imBuying: string;
  imSelling: string;
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
  propertyAddress: string;
  city: string;
  state: string;
  zip: string;
  beds: string;
  baths: string;
  sqft: string;
  selectArea: string;
  select: string;
  yes: string;
  no: string;
  inProgress: string;
  asap: string;
  oneToThreeMonths: string;
  threeToSixMonths: string;
  sixToTwelveMonths: string;
  justCurious: string;
  tellUsAboutTheProperty: string;
  describeYourDreamHome: string;
  tellUsAboutYourPropertyAndWhatYoureLookingFor: string;
  additionalNotes: string;
  notesPlaceholderSelling: string;
  notesPlaceholderBuying: string;
  notesPlaceholderBoth: string;
  addressAutocompletePoweredByGoogleMaps: string;
  whenLookingToBuy: string;
  whenLookingToSell: string;
  whenLookingToBuyOrSell: string;

  // Thank-you page defaults
  thankYouHeading: string;
  thankYouSubheading: string;
  thankYouBody: string;
  thankYouDisclaimer: string;
  thankYouCtaCall: string;
  thankYouCtaBack: string;
}

const uiStringsEn: UiStrings = {
  // Skip nav
  skipToMainContent: "Skip to main content",

  // Nav
  contactMe: "Contact Me",

  // Footer
  cellLabel: "Cell:",
  privacyPolicy: "Privacy Policy",
  termsOfUse: "Terms of Use",
  accessibility: "Accessibility",
  allRightsReserved: "All rights reserved.",
  licensedRealEstateSalesperson: "Licensed Real Estate Salesperson",

  // CmaSection
  getMyFreeHomeValueReport: "Get My Free Home Value Report \u2192",
  findMyDreamHome: "Find My Dream Home \u2192",
  getStarted: "Get Started \u2192",
  somethingWentWrong: "Something went wrong. Please try again.",

  // LeadForm labels
  imBuying: "I'm Buying",
  imSelling: "I'm Selling",
  firstName: "First Name",
  lastName: "Last Name",
  email: "Email",
  phone: "Phone",
  desiredArea: "Desired Area",
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
  selectArea: "Select area...",
  select: "Select...",
  yes: "Yes",
  no: "No",
  inProgress: "In Progress",
  asap: "ASAP",
  oneToThreeMonths: "1-3 Months",
  threeToSixMonths: "3-6 Months",
  sixToTwelveMonths: "6-12 Months",
  justCurious: "Just Curious",
  tellUsAboutTheProperty: "Tell us about the property",
  describeYourDreamHome: "Describe your dream home",
  tellUsAboutYourPropertyAndWhatYoureLookingFor: "Tell us about your property and what you're looking for",
  additionalNotes: "Additional Notes",
  notesPlaceholderSelling: "Recent renovations, unique features, timeline, price expectations...",
  notesPlaceholderBuying: "Must-haves, neighborhood preferences, school districts, budget flexibility...",
  notesPlaceholderBoth: "Describe your property for sale and what you're looking for in your next home...",
  addressAutocompletePoweredByGoogleMaps: "Address autocomplete powered by Google Maps.",
  whenLookingToBuy: "When are you looking to buy?",
  whenLookingToSell: "When are you looking to sell?",
  whenLookingToBuyOrSell: "When are you looking to buy/sell?",

  // Thank-you page defaults
  thankYouHeading: "Thank You!",
  thankYouSubheading: "Your Free Home Value Report Is Being Prepared Now!",
  thankYouBody: "{firstName} will send your personalized Comparative Market Analysis to your email shortly. Keep an eye on your inbox!",
  thankYouDisclaimer: "This home value report is a Comparative Market Analysis (CMA) and is not an appraisal. It should not be considered the equivalent of an appraisal.",
  thankYouCtaCall: "Call {firstName}: {phone}",
  thankYouCtaBack: "Back to {firstName}'s Site",
};

const localeMap: Record<string, UiStrings> = {
  en: uiStringsEn,
  es: uiStringsEs,
};

/** Get UI strings for a locale. Falls back to English for unsupported locales. */
export function getUiStrings(locale?: string): UiStrings {
  if (!locale) return uiStringsEn;
  return localeMap[locale] ?? uiStringsEn;
}
