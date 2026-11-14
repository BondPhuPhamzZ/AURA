// Serves only this folder's tool files on loopback so crypto.subtle (secure context) is available.
// Receipt images are opened from the browser's file picker and never pass through this server.
import { createReadStream } from "node:fs";
import { createServer } from "node:http";
import { fileURLToPath } from "node:url";
import { join } from "node:path";

const root = fileURLToPath(new URL("./", import.meta.url));
const files = new Map([
  ["/", ["index.html", "text/html; charset=utf-8"]],
  ["/index.html", ["index.html", "text/html; charset=utf-8"]],
  ["/annotation.css", ["annotation.css", "text/css; charset=utf-8"]],
  ["/annotation-core.js", ["annotation-core.js", "text/javascript; charset=utf-8"]],
  ["/annotation-app.js", ["annotation-app.js", "text/javascript; charset=utf-8"]]
]);

createServer((request, response) => {
  const entry = files.get(new URL(request.url ?? "/", "http://127.0.0.1").pathname);
  if (!entry || request.method !== "GET") {
    response.writeHead(404).end("Not found");
    return;
  }
  response.writeHead(200, { "Content-Type": entry[1], "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" });
  createReadStream(join(root, entry[0])).pipe(response);
}).listen(8766, "127.0.0.1", () => {
  console.log("AURA evidence annotation tool: http://127.0.0.1:8766/");
});
