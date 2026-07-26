const config = {
  extends: ["stylelint-config-standard"],
  ignoreFiles: ["app/tokens.css", ".next/**", "node_modules/**"],
  rules: {
    "color-no-hex": true,
    "declaration-block-no-redundant-longhand-properties": null,
    "selector-class-pattern": null,
    "custom-property-pattern": null
  }
};

export default config;
