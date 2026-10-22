# JS in production for ASP.NET Core MVC: minification, bundling, obfuscation - research

Date: 2026-10-22. Status: research for a decision, no implementation and no code changes.

Question: how do others handle JavaScript in MVC, when do they obfuscate versus just minify, and is this
project's direction sound. Dev should serve plain source, prod must not.

## 1. Summary

1. **Obfuscation is not the norm for an online shop.** The norm is minification (with name mangling), bundling,
   fingerprinting, Brotli/gzip compression and long-lived caching.
2. **Minification is not a security control.** Neither minification nor obfuscation protects anything. Secrets
   and business logic must stay on the server.
3. **The project targets net10.0 but does not use what ASP.NET Core provides out of the box** (`MapStaticAssets`:
   fingerprinting, build-time Brotli/gzip, `immutable`). `Startup.cs` still uses the older `UseStaticFiles()`.
4. **Own JS is not minified.** About 66 KB across 11 modules ships to prod as source.
   `frontend.instructions.md` says "Minify and bundle assets in production builds", but nothing implements it.
5. **AMD + require.js is a fading direction** (others are migrating to ES modules), but at this size it is not
   urgent.

## 2. Current state (verified in the code)

| Area | State | Where |
|---|---|---|
| Target | net10.0 | `ECommerceApp.Web.csproj` |
| Static files | `app.UseStaticFiles()`, no `MapStaticAssets`, no `UseResponseCompression` | `Startup.cs:193` |
| Libraries | LibMan, `*.min.js` files (jQuery, Bootstrap, Tom Select) | `libman.json`, `_Layout.cshtml` |
| Own JS | `wwwroot/js/*.js`, not minified | 11 AMD modules + `config.js`, `site.js` |
| Cache busting | `asp-append-version` on `config.js`/`site.js`; custom `urlArgs` from `JsVersionProvider` for AMD modules | `_Layout.cshtml`, `config.js` |
| Page scripts | inline in views (16 views use `DOMInitialized`) | `Views/**`, `Areas/**` |
| Bundler / minifier | none (no WebOptimizer, esbuild, Vite, terser) | - |

Consequence: scripts inline in `.cshtml` never pass through a file-based pipeline, and AMD modules load as many
separate requests, each in source form.

## 3. How others do it

Note: this is a survey of documentation, forums and READMEs, not a code analysis of many repositories. I found
no benchmark and no official Microsoft recommendation between the tools below.

### 3.1 Microsoft (ASP.NET Core 10 docs)

- "ASP.NET Core doesn't provide a native bundling and minification solution." It names Gulp and Webpack as
  external tools. Recommendation: minified files in prod, source files in dev, switched with the
  `<environment>` tag helper.
  Source: [Bundle and minify static assets](https://learn.microsoft.com/en-us/aspnet/core/client-side/bundling-and-minification)
- Since .NET 9, `MapStaticAssets` provides: SHA-256 fingerprinting at build time, gzip and Brotli compression at
  publish, `Cache-Control: immutable` for fingerprinted files, and content-based ETags. It does not minify.
  With `UseStaticFiles` alone these benefits do not apply.
  Source: [Serve static files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files)

### 3.2 Tools in the .NET ecosystem

| Tool | Model | Notes |
|---|---|---|
| **WebOptimizer** (LigerShark) | middleware, runtime minification and bundling, output cached in memory and on disk | NUglify, source maps, tag helpers with hash. About 146 open issues, maintenance status unclear. [GitHub](https://github.com/ligershark/WebOptimizer) |
| **BuildBundlerMinifier** | MSBuild + `bundleconfig.json` | older, I do not recommend it (maintenance status not verified) |
| **ESBuild.MSBuild** | esbuild run during `dotnet build`, output as static web assets, minify on by default in Release | no Node needed at runtime; closest to a long-term direction. [NuGet](https://www.nuget.org/packages/ESBuild.MSBuild) |
| **ESBuild.AspNetCore** | `esbuild.json`, binary shipped in the package | its README states the repo is 100% AI-generated code, be careful |
| **Vite/webpack/Gulp** | separate frontend step (Node in CI) | most capable, highest process cost |

### 3.3 Larger projects

- **nopCommerce** (MVC shop): JS/CSS bundling and minification toggled in settings, built on WebOptimizer.
  Forums show typical issues: turning bundling off does not turn minification off, and older versions lost
  `.min.js` files from bundles. So even a shop of this class does minification and bundling, not obfuscation.
  Sources: [performance guide](https://www.nopcommerce.com/de/nopcommerce-performance-guide),
  [minify thread](https://www.nopcommerce.com/boards/topic/93098/how-to-disable-js-minify-at-nop45)
- **OrchardCore**: the Resource Manager registers both a minified and a debug URL per script
  (`SetUrl(minified, unminified)`), switched by `ResourceDebugMode`, cache busting with a hash on by default,
  optional CDN. This is exactly the "dev = source, prod = min" pattern.
  Source: [OrchardCore Resources](https://docs.orchardcore.net/en/latest/reference/modules/Resources/)
- **TYPO3** (non-.NET) replaced RequireJS with native ES modules (v12-13). **OutSystems** is moving from AMD to
  ESM with esbuild. Sources: [TYPO3](https://docs.typo3.org/c/typo3/cms-core/main/en-us/Changelog/12.0/Deprecation-97057-DeprecateRequireJSSupport.html),
  [OutSystems](https://www.outsystems.com/forums/discussion/98518/odc-transitioning-from-requirejs-to-modern-javascript-bundler/)
  I found no official deprecation notice for RequireJS itself, only these migrations.

### 3.4 Minification vs obfuscation

- Minification: removes comments and whitespace, shortens local names. Reversible with a beautifier, not a
  security measure. The production baseline.
- Obfuscation: string encoding, control-flow flattening, dead code. Bigger files, slower execution, harder
  debugging, and risk of changed behavior (for example names used as strings: form fields, JSON keys, routes).
  Justified when the client holds something worth hiding: licensing, DRM, an anti-fraud algorithm, games.
  An academic study of real websites found advanced obfuscation affected under 1% of scripts; most
  transformed code was plain minification. [Anything to hide](https://www.doc.ic.ac.uk/~maffeis/331/docs/anything-to-hide.pdf)
  The remaining sources are mostly obfuscator vendor blogs (biased).
- For this project: the JS has no logic or secrets worth obfuscating (validation, AJAX, modals; price and
  payment are computed server-side). Obfuscation would add cost with no gain. I did not read all the JS for
  secrets; one grep for keys/tokens is worth doing during rollout.

## 4. Assessment of the current direction

**Good:**
- Modules with explicit dependencies and `DOMInitialized` without globals. Mangling will not break anything
  that is not referenced by a global name (exception: `window.PagerClick` and inline `onclick`, which stay
  explicit).
- Fingerprinting and pinned library versions in LibMan; libraries are already `.min`.
- Dev and prod can differ via the `<environment>` tag, available immediately.

**Gaps:**
1. No minification of own JS (and CSS) in prod.
2. No `MapStaticAssets` despite net10.0 (Brotli/gzip and `immutable` for free).
3. Inline view scripts bypass every pipeline and block a CSP without `unsafe-inline`.
4. AMD/require.js: many small files without a bundle; a fading technology. Caveat: no official deprecation,
   but a clear migration trend.

## 5. Options for the project

| | Option A: minimum | Option B: WebOptimizer | Option C: esbuild at build time |
|---|---|---|---|
| Scope | `MapStaticAssets` + Brotli + `<environment>`; own JS unminified or hand-made `.min` | runtime min + bundle, no build step | build-time min + bundle, output in `wwwroot/dist` or static web assets |
| Code change | very small | small, but AMD needs care (require loads by URL, `urlArgs`) | medium; best together with AMD -> ES modules migration |
| Inline scripts | untouched | untouched | must move to `wwwroot/js/pages/*.js` |
| New dependencies | none | NuGet (uncertain maintenance) | NuGet with esbuild, or Node in CI and `Dockerfile-web` |
| Risk | does not solve minification | dynamic `require` behavior to verify | largest scope, best long-term result |
| Obfuscation | no | no | no |

## 6. Recommendation (for your decision)

No obfuscation. A sensible order, each step can stop on its own:

1. Step 1 (small, low risk): switch from `UseStaticFiles` to `MapStaticAssets`, verify that
   `asp-append-version` and the AMD `urlArgs` still bust the cache correctly, enable Brotli.
2. Step 2: minify own JS in prod (Option B or C), dev keeps serving source.
3. Step 3 (separate decision, ADR): move inline scripts out and migrate AMD -> ES modules with a bundle.

## 7. What I did not verify

- I did not run any of these tools on this project, so compatibility with AMD/`require.js` is unverified.
- I did not check the current maintenance status of WebOptimizer (its docs still mention ASP.NET Core 2.x/3.0)
  or BuildBundlerMinifier.
- I did not read the source of nopCommerce or OrchardCore, only documentation and forums.
- I did not check whether `MapStaticAssets` works together with `UseOutputCache` and the current
  `JsVersionProvider` in this project.
- I did not measure the real gain from minification; at 66 KB of JS it may be small compared with compression
  and caching.
