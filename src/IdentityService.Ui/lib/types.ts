export type NavigationResponse = {
  navigation: { kind: "redirect" | "native-loading"; url: string };
};

export type SessionResponse = {
  isAuthenticated: boolean;
  displayName: string | null;
  isLocalRequest: boolean;
};

export type HomeResponse = {
  version: string;
  licenseConfigured: boolean;
  licenseSerialNumber: string | null;
  licenseExpiration: string | null;
};

export type ExternalProvider = {
  authenticationScheme: string;
  displayName: string;
  challengeUrl: string;
};

export type LoginContext = {
  returnUrl: string | null;
  username: string | null;
  enableLocalLogin: boolean;
  allowRememberLogin: boolean;
  externalProviders: ExternalProvider[];
  externalLoginUrl: string | null;
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

export type DeviceContext = {
  userCode: string | null;
  hasRequest: boolean;
  error: string | null;
  client: ClientContext | null;
  identityScopes: Scope[];
  apiScopes: Scope[];
};

export type CibaRequest = {
  id: string;
  client: ClientContext;
  bindingMessage: string | null;
};

export type PendingCibaRequest = {
  id: string;
  clientId: string;
  clientName: string | null;
  bindingMessage: string | null;
};

export type CibaConsentContext = {
  id: string;
  client: ClientContext;
  bindingMessage: string | null;
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

export type Diagnostics = {
  claims: Array<{ name: string; value: string | null }>;
  properties: Array<{ name: string; value: string | null }>;
  clients: string[];
};

export type UserSession = {
  subjectId: string;
  sessionId: string;
  displayName: string | null;
  created: string;
  expires: string | null;
  clientIds: string[];
};

export type Sessions = {
  enabled: boolean;
  results: UserSession[];
  resultsToken: string | null;
  hasPreviousResults: boolean;
  hasNextResults: boolean;
  totalCount: number | null;
  currentPage: number | null;
  totalPages: number | null;
};

export type ErrorContext = {
  error: string;
  errorDescription: string | null;
  requestId: string | null;
};

export type RedirectContext = { redirectUri: string };

export type ProblemDetails = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};
