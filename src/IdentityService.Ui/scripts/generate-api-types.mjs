import { readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import process from "node:process";
import openapiTS, { astToString } from "openapi-typescript";

const source = process.env.IDENTITY_UI_OPENAPI_URL ?? "http://127.0.0.1:5101/openapi/v1.json";
const outputPath = resolve(import.meta.dirname, "..", "lib", "api.generated.ts");
const ast = await openapiTS(new URL(source));
const generated =
  `/* Generated from the IdentityService development OpenAPI document. Do not edit. */\n${astToString(ast)}`;

if (process.argv.includes("--check")) {
  const current = await readFile(outputPath, "utf8").catch(() => "");
  if (current !== generated) {
    throw new Error("lib/api.generated.ts is stale. Run npm run api:generate while IdentityService is running.");
  }
} else {
  await writeFile(outputPath, generated, "utf8");
}
