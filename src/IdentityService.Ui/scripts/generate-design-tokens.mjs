import { readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import process from "node:process";
import YAML from "yaml";

const root = resolve(import.meta.dirname, "..");
const sourcePath = resolve(root, "..", "IdentityService", "DESIGN-bmw-m.md");
const outputPath = resolve(root, "app", "tokens.css");
const source = await readFile(sourcePath, "utf8");
const match = source.match(/^---\r?\n([\s\S]*?)\r?\n---/);

if (!match) {
  throw new Error(`No YAML front matter found in ${sourcePath}`);
}

const design = YAML.parse(match[1]);
const lines = [
  "/* Generated from src/IdentityService/DESIGN-bmw-m.md. Do not edit directly. */",
  ":root {",
  `  --design-version: "${design.version}";`,
];

const kebab = (value) =>
  value
    .replace(/([a-z0-9])([A-Z])/g, "$1-$2")
    .replace(/[^a-zA-Z0-9-]+/g, "-")
    .toLowerCase();

const groupPrefixes = {
  colors: "color",
  rounded: "radius",
  spacing: "space",
  motion: "motion",
};

const tokenValue = (value) => {
  if (typeof value !== "string") return String(value);
  return value.replace(/\{([a-zA-Z0-9-]+)\.([a-zA-Z0-9-]+)\}/g, (_, group, name) => {
    const prefix = groupPrefixes[group] ?? kebab(group);
    return `var(--${prefix}-${kebab(name)})`;
  });
};

for (const [groupName, group] of Object.entries({
  color: design.colors,
  radius: design.rounded,
  space: design.spacing,
  motion: design.motion,
})) {
  for (const [name, value] of Object.entries(group ?? {})) {
    lines.push(`  --${groupName}-${kebab(name)}: ${tokenValue(value)};`);
  }
}

for (const [name, values] of Object.entries(design.typography ?? {})) {
  for (const [property, value] of Object.entries(values)) {
    lines.push(`  --type-${kebab(name)}-${kebab(property)}: ${tokenValue(value)};`);
  }
}

for (const [name, values] of Object.entries(design.components ?? {})) {
  for (const [property, value] of Object.entries(values)) {
    if (property === "typography") continue;
    lines.push(`  --component-${kebab(name)}-${kebab(property)}: ${tokenValue(value)};`);
  }
}

lines.push("}", "");
const generated = `${lines.join("\n")}`;

if (process.argv.includes("--check")) {
  const current = await readFile(outputPath, "utf8").catch(() => "");
  if (current !== generated) {
    throw new Error("app/tokens.css is stale. Run npm run tokens:generate.");
  }
} else {
  await writeFile(outputPath, generated, "utf8");
}
