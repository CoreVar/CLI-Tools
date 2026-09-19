#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const directory = dirname(fileURLToPath(import.meta.url));
if (process.argv.slice(2).length === 1 && process.argv[2] === "--corevar-describe") {
  console.log(readFileSync(join(directory, "corevar.module.json"), "utf8"));
  process.exit(0);
}
const args = process.argv.slice(2);
console.log(`Hello, ${args[0] === "hello" && args[1] ? args[1] : "world"}!`);
