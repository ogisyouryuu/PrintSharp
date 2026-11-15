# Preview wire protocol v1

UTF-8 JSON, camelCase, null optional fields omitted. Coordinates use PDF points (1/72 inch), origin top-left, page numbers 1-based. `PrintSharp.Pdf.Preview.PreviewJson` serializes manifest/page/text responses without ASP.NET Core. All responses in a session must share documentId/revision.

| Method / route under configured base URL | Response |
|---|---|
| GET /{id} | Manifest |
| GET /{id}/pages/{pageNumber} | Page with page-local resources |
| GET /{id}/text | Text index |
| GET /{id}/pdf | application/pdf attachment |
| DELETE /{id} | 204, idempotent, only deletes owner-accessible entries |

Creation belongs to the host business API. PrintSharp.Api retains its Excel-template POST endpoints. All GETs validate ownership before caching. Manifest/page/text use `ETag: "{revision}-{part}"`, `Cache-Control: private, no-cache`, If-None-Match/304. PDF uses private, no-store. Errors are ProblemDetails with status/detail: 404 unavailable/wrong owner/out of range; 410 expired while entry remains; 413 document size limit; 503 capacity. Expiration sweep may turn 410 into 404.

Manifest: schemaVersion=1, documentId (opaque string), revision (opaque string), expiresAt (ISO-8601 UTC), pageCount (nonnegative integer), pages (ordered pageNumber/width/height). Dimensions must be positive finite numbers.

Page response: documentId, revision, page, resources. Page: schemaVersion=1, unit="pt", pageNumber, width, height, commands, textRegions. Resources: dictionary keyed by resourceId, values {kind,data}, containing everything that page uses.

The command discriminator is **op**, not type:

| op | values and other fields |
|---|---|
| save / restore | no values, balanced state stack |
| clipRect | [x,y,width,height] |
| fillRect | [x,y,width,height], color |
| line | [x1,y1,x2,y2], color, strokeWidth, optional dash lengths |
| image | [x,y,width,height], resourceId for png |
| glyphRun | color, glyphs: [{resourceId,x,y,size}] |

Color: #RRGGBBAA. Resource kind=path: SVG path data normalized to font size 1; glyph x/y is baseline origin and size scales the outline. Resource kind=png: base64 PNG bytes without a data-URL prefix. Browser needs no font files. Server-only PDF Unicode metadata is not serialized.

Text index: schemaVersion=1, documentId, revision, pages [{pageNumber,regions}]. Region: text, x/y/width/height, optional boxes. Box: start/length (UTF-16 indexes in region text), x/y/width/height, rtl boolean. Old/custom renderers may omit boxes; fall back to region bounds. Regions also appear in display pages. Search validates documentId/revision/expiry.

Compatibility: accept schemaVersion 1 and ignore unknown optional properties; reject unsupported major versions and unknown drawing operations/resources. Breaking field/interpretation changes and new mandatory operations/resource kinds require a new schemaVersion. Never mix revisions or cache across documents. Custom storage/routes should use PreviewJson to preserve the contract.

## Version 2 HTTP transport

Append `?schemaVersion=2` to manifest, page and text routes. Unversioned routes continue to return v1. The new HTTP source accepts a v1 response from older servers and normalizes supported v2 responses for the existing Canvas renderer.

- Manifest: same fields as v1, schemaVersion=2.
- Page response: documentId, revision, page (schemaVersion=2, otherwise unchanged), resourceIds. No inline resources.
- GET `/{id}/resources?ids=comma-separated-IDs`: schemaVersion=2, documentId, revision, resources dictionary. Requires the same owner and TTL as other routes; unknown IDs return 404, empty or more than 128 distinct IDs return 400. Clients batch at 64 IDs to stay below typical URL limits. Never mix documents or revisions.
- Text index: schemaVersion=2, same region text and bounds, no character boxes. Use loaded display-page boxes for precise highlights; otherwise fall back to region bounds.
- JSON responses negotiate gzip, with Vary: Accept-Encoding and representation-specific ETags. `gzip;q=0` overrides wildcard acceptance. No Brotli is required. Page, manifest and text gzip bytes are stored once; selected resource batches and legacy responses are constructed on demand.
- PDF route: optional filename query; sanitized Title is the default, document.pdf the fallback.

PreviewJson remains the v1 serialization API. PreviewEntry accepts its v1 constructor inputs and converts them to compressed v2 storage; Pages/Manifest/TextIndex are compatibility projections, Size counts stored bytes. Use PageCount, Page(number,v2,gzip), ManifestBytes(v2,gzip), Text(v2,gzip), Resources(ids,gzip) for transport. A distributed store should preserve ownership, expiry and immutable revision and can reconstruct an entry from v1 serialized data. This release changes PreviewEntry from a positional record to an immutable class; custom stores using record `with`/deconstruction should migrate to its constructor.
