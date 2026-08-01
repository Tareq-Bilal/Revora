export type NavigationResponse = {
  navigation: { kind: "redirect"; url: string };
};

export type SessionResponse = {
  isAuthenticated: boolean;
  displayName: string | null;
};

export type LoginContext = {
  returnUrl: string | null;
  username: string | null;
  enableLocalLogin: boolean;
  allowRememberLogin: boolean;
};

export type RegisterContext = {
  returnUrl: string | null;
  passwordRequirements: {
    requiredLength: number;
    requiredUniqueChars: number;
    requireDigit: boolean;
    requireLowercase: boolean;
    requireUppercase: boolean;
    requireNonAlphanumeric: boolean;
  };
};

export type LogoutContext = {
  logoutId: string | null;
  showPrompt: boolean;
  autoSubmit: boolean;
};

export type LoggedOutContext = {
  clientName: string | null;
  postLogoutRedirectUri: string | null;
  signOutIframeUrl: string | null;
  automaticRedirectAfterSignOut: boolean;
};

export type Resource = { name: string | null; displayName: string | null };

export type Scope = {
  name: string | null;
  value: string;
  displayName: string;
  description: string | null;
  emphasize: boolean;
  required: boolean;
  checked: boolean;
  resources: Resource[];
};

export type ClientContext = {
  clientName: string | null;
  clientUrl: string | null;
  clientLogoUrl: string | null;
};

export type ConsentContext = {
  returnUrl: string;
  client: ClientContext;
  allowRememberConsent: boolean;
  identityScopes: Scope[];
  apiScopes: Scope[];
};

export type Grant = {
  clientId: string;
  clientName: string;
  clientUrl: string | null;
  clientLogoUrl: string | null;
  description: string | null;
  created: string;
  expires: string | null;
  identityGrantNames: string[];
  apiGrantNames: string[];
};

export type ErrorContext = {
  error: string;
  errorDescription: string | null;
  requestId: string | null;
};

export type ProblemDetails = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};
