import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { createServer } from "node:http";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("./", import.meta.url));
const contentTypes = new Map([
  [".html", "text/html; charset=utf-8"],
  [".svg", "image/svg+xml; charset=utf-8"],
  [".md", "text/markdown; charset=utf-8"]
]);

createServer(async (request, response) => {
  const requestPath = decodeURIComponent(new URL(request.url ?? "/", "http://127.0.0.1").pathname);
  const relativePath = requestPath === "/" ? "index.html" : requestPath.replace(/^\/+/, "");
  const resolved = normalize(join(root, relativePath));
  if (!resolved.startsWith(root)) {
    response.writeHead(403).end("Forbidden");
    return;
  }
  try {
    const file = await stat(resolved);
    if (!file.isFile()) throw new Error("Not a file");
    response.writeHead(200, {
      "Content-Type": contentTypes.get(extname(resolved)) ?? "application/octet-stream",
      "Cache-Control": "no-store"
    });
    createReadStream(resolved).pipe(response);
  } catch {
    response.writeHead(404).end("Not found");
  }
}).listen(8765, "127.0.0.1", () => {
  console.log("Evidence overlay prototype: http://127.0.0.1:8765/");
});
