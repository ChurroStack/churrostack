window.__ENV__ = {
  VITE_OIDC_ISSUER_URI: "$VITE_OIDC_ISSUER_URI",
  APP_NAME: "$APP_NAME",
};

if (window.__ENV__.APP_NAME && window.__ENV__.APP_NAME.length > 0 && window.__ENV__.APP_NAME[0] !== "$") {
  document.title = window.__ENV__.APP_NAME;
}