# CSS_RULES.md (Named Style Library)

This file is the concrete styling layer. `WEB_DESIGN_RULES.md` defines the shared component patterns, layout rules, and implementation shapes every build uses; this file defines the 13 named *looks* those patterns can be dressed in — one per reference image in the approved gallery. Each style is a self-contained palette + typography + decoration + radius/shadow recipe, not a new component system.

`FRONTEND_RULES.md` and `WEB_DESIGN_RULES.md` both defer to this file for the concrete visual identity of a build. `AI_CODING_RULES.md` mandates reading this file before starting frontend/UI work on a new project.

---

## 0. How the Agent Must Use This File

- **Before writing any UI for a new project** (a new app, a new marketing site, a new page family with no established visual identity yet), the agent asks the user which named style below to use — do not pick one silently and do not default to a blend.
  - Suggested prompt: *"Which style should I use — [list the 13 names], a mix of specific ones, or a fully original direction?"*
- **If the project already has an established style** (an existing `globals.css`/theme file, or a style chosen earlier in the same project's history), use that — do not re-ask on every session, and do not silently switch styles mid-project.
- **Mixing is opt-in only.** Only combine two or more named styles, or invent a new original direction, when the user explicitly says so ("mix Verdant and Vortek," "something original," "surprise me"). Otherwise apply exactly one named style with no cross-contamination from the others.
- **This file governs palette, typography treatment, decoration, and radius/shadow only.** Component structure, established design-system usage, accessibility, responsive rules, and the hero/component pattern *shapes* still come from `WEB_DESIGN_RULES.md` — a chosen style fills in the tokens, it doesn't change which component is used.
- **Encode palette values as theme variables/tokens.** Hex values below are source values for the selected theme, not permission to scatter inline hex colors through components.
- **Light/dark mode still applies regardless of style.** Every style below lists both a light and dark treatment; the **Light / Dark Mode — Always Present** rule in `WEB_DESIGN_RULES.md` is still mandatory no matter which named style is chosen.

---

## 1. Adventure

**Mood:** bold, editorial, outdoor-adventure travel.

**When to offer this:** travel, outdoor/adventure brands, expedition or hospitality products, anything wanting a rugged, confident tone.

**Palette:**
- Dark (native): full-bleed photography with a `bg-gradient-to-t from-black/85 via-black/25 to-transparent` scrim, `text-white` copy, one warm accent (amber/yellow, `~#F2B90F`) reserved for a single icon-button.
- Light equivalent (when default-light is required): `bg-background` white base for non-hero sections, same amber accent on `bg-primary`, hero remains photographic/dark as the one exempted surface under **Modern Visual Design Language** in `WEB_DESIGN_RULES.md`.

**Typography:** condensed, heavy-weight uppercase headline (`uppercase font-bold tracking-tight`), thin one-line eyebrow preceded by a short horizontal rule (`h-px w-8 bg-white/40`).

**Radius & shadow:** `rounded-xl` on carousel thumbnails, `rounded-full` on nav pill and icon buttons, no drop shadow on the hero (photography provides the depth).

**Signature decoration:** bottom-right thumbnail carousel bleeding off the frame edge, circular prev/next arrows, two-digit counter (`01`) with a thin progress rule.

**Hero pattern:** Pattern A (full-bleed photographic).

**Key component notes:** primary CTA is a solid pill button; secondary is a circular icon-only button in the accent color sitting beside it, not below it.

**Code reference:**
```tsx
<section className="relative flex h-screen items-end overflow-hidden">
  <Image src="/hero-adventure.jpg" alt="" fill className="object-cover" />
  <div className="absolute inset-0 bg-gradient-to-t from-black/85 via-black/25 to-transparent" />

  <div className="relative z-10 w-full px-8 pb-16 text-white sm:px-16">
    <div className="mb-4 flex items-center gap-3">
      <span className="h-px w-8 bg-white/40" />
      <span className="text-sm uppercase tracking-widest text-white/70">
        Embark On The Journey Of A Lifetime
      </span>
    </div>
    <h1 className="max-w-xl text-5xl font-bold uppercase leading-tight tracking-tight sm:text-6xl">
      Travel Far, Find Yourself
    </h1>
    <p className="mt-4 max-w-md text-sm text-white/70">
      Explore pristine beaches and lush rainforests while immersing yourself in local cultures.
    </p>
    <div className="mt-8 flex items-center gap-3">
      <Button size="icon" className="rounded-full bg-amber-400 text-black hover:bg-amber-300">
        <Bookmark className="h-4 w-4" />
      </Button>
      <Button className="rounded-full">Start Your Adventure</Button>
    </div>
  </div>

  {/* bottom-right thumbnail carousel + counter */}
  <div className="absolute bottom-16 right-8 z-10 hidden sm:block">
    <Carousel opts={{ align: "start" }} className="w-[420px]">
      <CarouselContent className="ml-0 gap-3">
        {thumbnails.map((t) => (
          <CarouselItem key={t.id} className="basis-28 pl-0">
            <Image src={t.src} alt="" width={112} height={140} className="h-36 w-28 rounded-xl object-cover" />
          </CarouselItem>
        ))}
      </CarouselContent>
      <div className="mt-4 flex items-center gap-3">
        <CarouselPrevious className="static h-9 w-9 translate-y-0 rounded-full border-white/40 text-white" />
        <CarouselNext className="static h-9 w-9 translate-y-0 rounded-full border-white/40 text-white" />
        <div className="h-px flex-1 bg-white/30" />
        <span className="font-mono text-sm">01</span>
      </div>
    </Carousel>
  </div>
</section>
```

---

## 2. AI_solutions

**Mood:** minimal, confident, monochrome enterprise SaaS.

**When to offer this:** B2B SaaS, dev tools, internal platforms, anything wanting to feel serious and understated rather than playful.

**Palette:**
- Dark (native): base `#0a0a0a`–`#0d0d0d`, cards `#141414` with `border-white/10`, **no saturated accent color** — contrast and whitespace do the work; buttons are white-on-dark or dark-on-white, not a brand hue.
- Light equivalent: `bg-background` white, cards `bg-card` with a hairline border, buttons `bg-primary` in a single neutral-adjacent brand color if one exists, otherwise near-black.

**Typography:** clean grotesque sans, medium weight throughout (avoid ultra-bold), generous line-height on subcopy, small muted secondary text under every headline.

**Radius & shadow:** `rounded-2xl` cards, `rounded-full` buttons, `shadow-none` + border on all cards — this style is the flattest/most border-led of the 13.

**Signature decoration:** a single soft radial glow behind the hero headline; a dashboard/product-screenshot card with a line-chart and a floating tooltip label as the hero's visual anchor.

**Hero pattern:** Pattern B (centered SaaS).

**Key component notes:** 3-tier pricing row with the middle tier lifted to a lighter card background plus a "Popular" `Badge` and a checkmark feature list (the **Component Patterns** pricing example in `WEB_DESIGN_RULES.md`); a secondary 4-across icon+text row with no card border for lightweight supporting features.

**Code reference:**
```tsx
<section className="relative overflow-hidden bg-[#0a0a0a] px-8 py-24 text-center text-white sm:py-32">
  <div className="pointer-events-none absolute inset-x-0 top-0 mx-auto h-64 w-64 -translate-y-1/2 rounded-full bg-white/10 blur-3xl" />
  <h1 className="mx-auto max-w-2xl text-4xl font-semibold tracking-tight sm:text-5xl">
    AI Solutions Engineered for Maximum Performance
  </h1>
  <p className="mx-auto mt-4 max-w-lg text-sm text-white/60">
    Discover intelligent tools that streamline operations, reduce manual work, and help your business move faster.
  </p>
  <Button className="mt-8 rounded-full bg-white text-black hover:bg-white/90">
    Explore More <ArrowUpRight className="ml-1 h-4 w-4" />
  </Button>
</section>

{/* feature card */}
<Card className="border-white/10 bg-[#141414] p-6 text-white shadow-none">
  <div className="mb-4 flex h-10 w-10 items-center justify-center rounded-lg bg-white/10">
    <Icon className="h-5 w-5" />
  </div>
  <CardTitle className="text-white">Automated reporting</CardTitle>
  <CardDescription className="text-white/50">
    Automated reporting gives you fast, accurate insights with zero manual effort.
  </CardDescription>
</Card>

{/* featured pricing tier */}
<Card className="border-white/20 bg-[#1c1c1c] text-white shadow-lg sm:scale-105">
  <CardHeader>
    <Badge className="w-fit bg-white text-black">Popular</Badge>
    <CardTitle className="text-white">Standard</CardTitle>
  </CardHeader>
  <CardContent className="space-y-4">
    <div className="text-4xl font-bold">
      $85<span className="text-sm font-normal text-white/50">/user/month</span>
    </div>
    <ul className="space-y-2 text-sm text-white/70">
      <li className="flex items-center gap-2"><Check className="h-4 w-4" /> Unlimited projects</li>
      <li className="flex items-center gap-2"><Check className="h-4 w-4" /> AI integration</li>
    </ul>
  </CardContent>
  <CardFooter>
    <Button className="w-full bg-white text-black hover:bg-white/90">Get Started</Button>
  </CardFooter>
</Card>
```

---

## 3. Build_together

**Mood:** vibrant, energetic creative agency.

**When to offer this:** agencies, portfolios, product launches, anything wanting a "people-first, high-energy" feel — this is the one deliberately gradient-forward, full-page-color style in the set.

**Palette:**
- Native treatment: coral/salmon (`~#F2A7A0`) at the top fading through near-black into a deep red glow (`~#7A1B10`) at the bottom of the page — this is the one style where a full-page gradient background is the default, not an accent.
- Light equivalent: if the user wants this energy without the full gradient background, keep the gradient confined to the CTA button and the "Free Test Drive" media panel, with the rest of the page on `bg-background` white.

**Typography:** rounded geometric bold sans, tight all-caps hero headline, casual/friendly secondary type for section labels ("Energistically," "Syndicate").

**Radius & shadow:** `rounded-2xl`–`rounded-3xl` on cards and the media panel, fully circular avatar photos, soft glow-shadow rather than a hard shadow under floating elements.

**Signature decoration:** floating circular avatar portraits connected by thin curved path lines with small gradient dot accents — the clearest "constellation of people" motif in the gallery; a gradient pill CTA ("Watch Reel") overlapping the imagery.

**Hero pattern:** Pattern C (split hero with floating social proof).

**Key component notes:** stat cards use a dark translucent card background even on the gradient section; a glass overlay stat badge (`$50M+`) sits at the bottom edge of the red media panel.

**Code reference:**
```tsx
<section className="relative overflow-hidden rounded-b-3xl bg-gradient-to-b from-[#f2a7a0] via-[#1a0a08] to-[#7a1b10] px-8 py-24 text-white sm:px-16">
  <h1 className="text-6xl font-extrabold uppercase leading-none tracking-tight">
    Let&apos;s Build<br />Together
  </h1>
  <p className="mt-4 max-w-sm text-sm text-white/70">
    We work collaboratively to bring your vision to life and ensure every project succeeds.
  </p>
  <Button className="mt-8 rounded-full bg-white text-black hover:bg-white/90">Get Started</Button>

  {/* floating avatar constellation */}
  <div className="pointer-events-none absolute right-12 top-16 hidden sm:block">
    <Avatar className="h-20 w-20 ring-4 ring-[#e94f4f]">
      <AvatarImage src="/people/1.jpg" alt="" />
    </Avatar>
    <Button className="mt-4 gap-2 rounded-full bg-gradient-to-r from-fuchsia-500 to-purple-600 text-white">
      <Play className="h-4 w-4" /> Watch Reel
    </Button>
  </div>
</section>

{/* stat card row */}
<Card className="border-white/10 bg-black/30 p-6 text-white backdrop-blur-sm">
  <div className="text-4xl font-bold">42<span className="align-top text-lg">%</span></div>
  <p className="mt-1 text-sm text-white/60">
    Quidam officiis similique sea ei, vel tollit indoctum efficiendi nihil.
  </p>
</Card>

{/* glass overlay badge on the media panel */}
<div className="absolute inset-x-6 bottom-6 flex items-center justify-between rounded-2xl bg-white/10 p-4 backdrop-blur-md">
  <div>
    <div className="text-2xl font-bold text-white">$50M+</div>
    <p className="text-xs text-white/60">Quidam officiis similique sea ei</p>
  </div>
  <div className="flex h-9 w-9 items-center justify-center rounded-full bg-white text-black">✦</div>
</div>
```

---

## 4. Discover_digital_art

**Mood:** colorful, creative, consumer-facing marketplace (NFT/digital art but generalizable to any creator marketplace).

**When to offer this:** marketplaces, creator platforms, portfolios, consumer apps wanting a playful multi-color accent instead of one restrained brand hue.

**Palette:**
- Dark (native): near-black base, a lime-to-green gradient accent (`~#D4E157` → `~#6B8E23`) for CTAs and decorative blobs, secondary pink/blue tones only inside avatar photos, not in UI chrome.
- Light equivalent: white base, same lime-green gradient reserved for the primary CTA and category tags, decorative blobs reduced to low opacity (per the **Concrete Style Tokens** light-mode opacity rule in `WEB_DESIGN_RULES.md`).

**Typography:** clean rounded sans, medium-weight body, large but not shouty headline (mixed-case, not uppercase).

**Radius & shadow:** `rounded-2xl` on the portrait image and the stats panel, `rounded-xl` on the trending-item grid cards.

**Signature decoration:** large soft color-gradient blobs behind the hero portrait; a circular rotating badge/ring of repeated brand text; color-tinted overlays on the trending-item thumbnails.

**Hero pattern:** a portrait-bleed variant of Pattern B/C — large single portrait bleeding off the frame edge with headline and dual CTA to its left.

**Key component notes:** stats live in one translucent rounded panel (count + label pairs) directly under the hero, not in separate `Card`s; avatar stack for social proof; "See all →" link convention for section overflow.

**Code reference:**
```tsx
<section className="relative overflow-hidden bg-[#0d0d0d] px-8 py-20 text-white sm:px-16">
  <div className="absolute -right-10 top-10 -z-10 h-72 w-72 rounded-full bg-gradient-to-br from-lime-400 to-green-600 opacity-30 blur-3xl" />
  <h1 className="max-w-lg text-5xl font-semibold leading-tight">
    Discover Digital Art and Collect NFTs.
  </h1>
  <p className="mt-4 max-w-md text-sm text-white/60">
    EnDasmu is a shared liquidity NFT market smart contract used by multiple sites.
  </p>
  <div className="mt-6 flex items-center gap-6">
    <Button className="rounded-full bg-gradient-to-r from-lime-400 to-green-500 text-black hover:opacity-90">
      Get Started
    </Button>
    <Button variant="link" className="gap-1 text-white">
      Learn More <ArrowRight className="h-4 w-4" />
    </Button>
  </div>
</section>

{/* stats translucent panel */}
<div className="flex flex-wrap items-center justify-between gap-6 rounded-2xl bg-white/5 p-6 backdrop-blur-md">
  <div className="flex gap-10">
    <div><div className="text-sm text-white/50">Artwork</div><div className="text-2xl font-bold text-white">27k+</div></div>
    <div><div className="text-sm text-white/50">Auction</div><div className="text-2xl font-bold text-white">25k+</div></div>
    <div><div className="text-sm text-white/50">Artist</div><div className="text-2xl font-bold text-white">12k+</div></div>
  </div>
  <div className="flex -space-x-3">
    {avatars.map((a) => (
      <Avatar key={a.id} className="h-9 w-9 ring-2 ring-[#0d0d0d]">
        <AvatarImage src={a.src} alt="" />
      </Avatar>
    ))}
  </div>
</div>
```

---

## 5. Greater_photo

**Mood:** cinematic, elegant, editorial course/media platform.

**When to offer this:** creative education, photography/film/design courses, portfolio-driven media brands.

**Palette:**
- Dark (native): warm dusk photography as the hero, near-black semi-transparent stat pills, white text, a single gold/amber underline accent for the active state in the numbered list.
- Light equivalent: white base for non-hero sections, same amber accent used sparingly on links/underlines, hero stays photographic.

**Typography:** an elegant serif or serif-adjacent display face for the hero title (a deliberate exception to the grotesque-sans default elsewhere in the gallery), small-caps tracked eyebrow text with a centered dot separator (`· Name ·`).

**Radius & shadow:** `rounded-lg` on the stat/tag pills, `rounded-2xl` on the class/category grid cards, circular play button with no shadow (sits directly on the photo).

**Signature decoration:** centered circular play button over the hero image; a horizontal numbered feature list (`01`–`04`) with a thin underline that highlights the active/current item.

**Hero pattern:** a centered variant of Pattern A — full-bleed photographic, but content is centered rather than left-aligned, anchored by the play button.

**Key component notes:** class/category cards pair a portrait image with a title and a small author byline underneath, not a description paragraph.

**Code reference:**
```tsx
<section className="relative flex h-[90vh] items-center justify-center overflow-hidden text-center text-white">
  <Image src="/hero-dusk.jpg" alt="" fill className="object-cover" />
  <div className="absolute inset-0 bg-black/30" />
  <div className="relative z-10 flex flex-col items-center">
    <Button size="icon" variant="secondary" className="mb-6 h-14 w-14 rounded-full bg-white/90 text-black hover:bg-white">
      <Play className="h-5 w-5" />
    </Button>
    <p className="text-xs uppercase tracking-[0.3em] text-white/70">· Daniel Kordan ·</p>
    <h1 className="mt-3 font-serif text-5xl italic">Landscape Photography</h1>
    <div className="mt-6 flex divide-x divide-white/20 overflow-hidden rounded-lg bg-black/50 text-xs">
      <div className="px-4 py-2"><div className="font-semibold">149 €</div><div className="text-white/50">Price</div></div>
      <div className="px-4 py-2"><div className="font-semibold">Expert</div><div className="text-white/50">Level</div></div>
      <div className="px-4 py-2"><div className="font-semibold">56H</div><div className="text-white/50">Length</div></div>
      <div className="px-4 py-2"><div className="font-semibold">14</div><div className="text-white/50">Lessons</div></div>
    </div>
  </div>
</section>

{/* numbered list with active-state underline */}
<div className="flex items-center gap-8 border-b border-white/10 pb-4 text-xs text-white/50">
  {steps.map((s, i) => (
    <div
      key={s.id}
      className={cn(
        "flex items-center gap-2 pb-4",
        i === activeIndex && "border-b-2 border-amber-400 text-white"
      )}
    >
      <span className="rounded-full border border-current px-2 py-0.5">{String(i + 1).padStart(2, "0")}</span>
      {s.label}
    </div>
  ))}
</div>
```

---

## 6. Saint_antoinet

**Mood:** identical family to Adventure — alpine/outdoor travel, slightly more restrained.

**When to offer this:** same use cases as Adventure; choose this one when the brand wants the same grammar without the yellow accent icon button, or when a simple circular location-pin CTA fits better than a bookmark icon.

**Palette:** same dark-photo-with-scrim treatment as Adventure, but with no separate accent color for the CTA icon — the primary button is a plain outlined/circular pin+label pairing instead of a colored icon chip.

**Typography:** same bold uppercase headline convention as Adventure, with a location-name eyebrow ("Switzerland Alps") instead of a motivational tagline.

**Radius & shadow:** identical to Adventure — `rounded-xl` thumbnails, `rounded-full` nav and icon buttons.

**Signature decoration:** same bottom-carousel + circular arrows + two-digit counter as Adventure — treat Adventure and Saint_antoinet as two skins of the same underlying hero recipe, differing mainly in the CTA/eyebrow treatment.

**Hero pattern:** Pattern A.

**Key component notes:** if the user picks this over Adventure, keep the accent color completely out of the CTA — a monochrome white/outline button is the differentiator.

**Code reference:**
```tsx
{/* Same hero shell as Adventure — swap the CTA row for the outline pin+label pairing */}
<div className="mt-8 flex items-center gap-3">
  <Button variant="outline" className="gap-2 rounded-full border-white/50 bg-transparent text-white hover:bg-white/10">
    <MapPin className="h-4 w-4" /> Discover Location
  </Button>
</div>

{/* eyebrow becomes a location tag instead of a motivational line */}
<span className="text-sm uppercase tracking-widest text-white/70">Switzerland Alps</span>
<h1 className="max-w-xl text-5xl font-bold uppercase leading-tight tracking-tight sm:text-6xl">
  Saint Antönien
</h1>
```

---

## 7. The_digital_frontier

**Mood:** monochrome, futuristic, high-tech editorial.

**When to offer this:** VR/AR, deep tech, futuristic product launches, brands wanting a stark black-and-white editorial tone with zero color accent.

**Palette:**
- Dark (native): pure near-black to charcoal gradient, white text, **fully monochrome — no accent color at all**, light-gray hairline borders only.
- Light equivalent: white base, near-black text and borders, same zero-accent-color discipline carried into light mode (buttons are solid black/white, not a brand hue).

**Typography:** bold condensed uppercase display headline overlapping the hero image, small pill tags in a row directly beneath it.

**Radius & shadow:** `rounded-2xl` on service cards and the feature image block, fully circular icon badges centered at the top of each service card.

**Signature decoration:** grainy, high-contrast portrait photography; a pill/tag row under the headline (topic chips); a vertical text-label list ("Innovation / Technology / Experience") beside a feature image instead of a paragraph.

**Hero pattern:** Pattern D (editorial/cinematic).

**Key component notes:** testimonial cards are stacked vertically with the avatar right-aligned (mirrors the more common left-aligned avatar pattern used elsewhere) — keep this mirrored layout only when this style is selected, not as a new default.

**Code reference:**
```tsx
<section className="relative overflow-hidden bg-black px-8 py-24 text-white sm:px-16">
  <h1 className="text-6xl font-bold uppercase leading-none tracking-tight">
    The Digital Frontier
  </h1>
  <div className="mt-4 flex gap-2">
    {["Digital", "Reality", "Next"].map((t) => (
      <Badge key={t} variant="outline" className="rounded-full border-white/30 text-white/70">
        {t}
      </Badge>
    ))}
  </div>
  <p className="mt-4 max-w-md text-sm text-white/60">
    Step into The Digital Frontier, where the boundaries between reality and virtual innovation disappear.
  </p>
  <div className="mt-6 flex items-center gap-4">
    <Button variant="secondary" className="rounded-md bg-white/10 text-white hover:bg-white/20">
      Learn More
    </Button>
    <Button variant="ghost" className="gap-2 text-white/70 hover:text-white">
      <PlayCircle className="h-4 w-4" /> Watch a Video
    </Button>
  </div>
</section>

{/* service card — centered circular icon badge */}
<Card className="border-white/10 bg-[#111] p-8 text-center text-white">
  <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-full border border-white/20">
    <Headset className="h-5 w-5" />
  </div>
  <CardTitle className="text-white">Reality Development</CardTitle>
  <CardDescription className="mt-2 text-white/50">
    Step into the future with our custom VR development services.
  </CardDescription>
  <Button variant="link" className="mt-4 text-white">Learn More</Button>
</Card>

{/* right-aligned avatar testimonial */}
<Card className="border-white/10 bg-[#141414] p-6 text-white">
  <p className="text-sm text-white/70">
    NeoVision completely transformed the way I interact with virtual reality.
  </p>
  <div className="mt-4 flex items-center justify-end gap-3">
    <div className="text-right">
      <div className="text-sm font-medium">James Rizaki</div>
    </div>
    <Avatar className="h-9 w-9"><AvatarImage src="/people/james.jpg" alt="" /></Avatar>
  </div>
</Card>
```

---

## 8. The_operating_system (StriveOS)

**Mood:** athletic, high-performance, motion-driven fitness/dashboard SaaS.

**When to offer this:** fitness, performance analytics, wearables, anything wanting energy conveyed through photography motion rather than color.

**Palette:**
- Dark (native): base `#0d0d0d`, cards `#171717`, no saturated UI accent — warmth comes from the photography (floral/motion tones), not from buttons or chips, which stay white/gray.
- Light equivalent: white base, cards with hairline borders, buttons solid black/near-black, photography carries the same motion-blur treatment.

**Typography:** clean grotesque sans, medium-to-bold headline, oversized gray numerals (`1`, `2`, `3`) for the process-step section — bigger and more muted than a typical stat number.

**Radius & shadow:** `rounded-2xl` on photo tiles, slightly less rounded buttons than the rest of the gallery (`rounded-md`, not `rounded-full`) — this style's one deliberate radius exception.

**Signature decoration:** motion-blur athletic photography throughout; a trust-logo row placed directly under the hero (Nike/Red Bull/etc.); an asymmetric bento grid mixing one large tile with two smaller ones.

**Hero pattern:** Pattern A, composed with less scrim than Adventure/Saint_antoinet — text sits over a naturally darker part of the photo rather than a heavy gradient.

**Key component notes:** numbered-process-step recipe (oversized numeral + title + description) alongside a tall photo; photo-backed testimonial cards; final CTA band on a full photo background before the footer.

**Code reference:**
```tsx
<section className="relative h-[80vh] overflow-hidden rounded-b-2xl bg-[#0d0d0d]">
  <Image src="/hero-cyclist.jpg" alt="" fill className="object-cover" />
  <div className="absolute inset-0 bg-gradient-to-r from-black/70 to-transparent" />
  <div className="relative z-10 flex h-full max-w-lg flex-col justify-center gap-4 px-8 text-white">
    <h1 className="text-4xl font-bold leading-tight">
      The Operating System for Human Performance
    </h1>
    <p className="text-sm text-white/60">
      AI-powered insights and community-driven challenges that turn every step, sprint, and rep into measurable progress.
    </p>
    <Button className="w-fit rounded-md bg-white text-black hover:bg-white/90">Start Free Trial</Button>
  </div>
</section>

{/* trust logo row directly under the hero */}
<div className="flex items-center gap-10 border-y border-white/10 bg-[#0d0d0d] px-8 py-4">
  <span className="text-xs uppercase tracking-widest text-white/40">Trusted by Athletes from:</span>
  {logos.map((l) => (
    <img key={l} src={`/logos/${l}.svg`} className="h-5 grayscale opacity-60 transition-opacity hover:opacity-100" />
  ))}
</div>

{/* oversized-numeral process step */}
<div className="flex items-start gap-4">
  <span className="text-6xl font-bold text-white/10">1</span>
  <div>
    <h3 className="text-lg font-semibold text-white">Track</h3>
    <p className="text-sm text-white/50">Sync your wearables and workouts seamlessly.</p>
  </div>
</div>
```

---

## 9. Transparent_Netflix

**Mood:** cinematic, glassy, entertainment/streaming.

**When to offer this:** media, entertainment, streaming, any product with a strong "watch/preview" moment as the primary action.

**Palette:**
- Dark (native): cool gray-blue near-black cinematic grade, white text, glass/translucent bars (`bg-background/10 backdrop-blur-lg border border-white/10`).
- Light equivalent: this style is inherently dark-hero by nature (cinematic photography); non-hero surfaces (browse grids, settings) still default to white per **Modern Visual Design Language** in `WEB_DESIGN_RULES.md`, with the same glass-bar treatment reserved for overlays on imagery only.

**Typography:** bold condensed sans title directly over the image, small meta-info row underneath (creator/stars/genre as inline `Badge`-style tags).

**Radius & shadow:** minimal radius on the hero itself (edge-to-edge image), `rounded-md` on buttons and thumbnails — the least-rounded style in the set, intentionally sharper for a "broadcast" feel.

**Signature decoration:** a frosted glass bar at the bottom of the viewport showing running counts ("Continue Watching 12," "My List 7"); a small preview thumbnail with a "Watch Trailer" label overlay; dot-style carousel indicators at the top instead of a numbered counter.

**Hero pattern:** Pattern D.

**Key component notes:** dual CTA is a solid "Play" button plus an outline icon-only button (heart/save), not a text link — the pairing is solid+icon-outline rather than solid+ghost-text.

**Code reference:**
```tsx
<section className="relative h-screen overflow-hidden">
  <Image src="/hero-witcher.jpg" alt="" fill className="object-cover" />
  <div className="absolute inset-0 bg-gradient-to-r from-black/80 via-black/30 to-transparent" />
  <div className="relative z-10 flex h-full max-w-md flex-col justify-center gap-3 px-12 text-white">
    <h1 className="text-5xl font-bold uppercase tracking-tight">The Witcher</h1>
    <p className="text-sm text-white/70">
      Geralt of Rivia, a solitary monster hunter, struggles to find his place in a world where people often prove more wicked than beasts.
    </p>
    <div className="mt-2 flex items-center gap-3">
      <Button className="rounded-md bg-white text-black hover:bg-white/90">
        <Play className="mr-2 h-4 w-4" /> Play
      </Button>
      <Button size="icon" variant="outline" className="rounded-md border-white/30 text-white">
        <Heart className="h-4 w-4" />
      </Button>
    </div>
  </div>

  {/* frosted glass bottom stat bar */}
  <div className="absolute inset-x-0 bottom-0 flex divide-x divide-white/10 border-t border-white/10 bg-white/5 backdrop-blur-lg">
    {[["Continue Watching", 12], ["My List", 7], ["Latest", 32]].map(([label, count]) => (
      <div key={label as string} className="flex-1 px-8 py-4 text-white">
        <span className="text-sm text-white/60">{label}</span>
        <span className="ml-2 font-semibold">{count}</span>
      </div>
    ))}
  </div>
</section>
```

---

## 10. Travel

**Mood:** clean, utilitarian, nature-forward booking/discovery product.

**When to offer this:** booking platforms, marketplaces, discovery-first products where "search/filter" is the primary hero action rather than a single CTA.

**Palette:**
- Dark (native): dark forest-green/near-black photographic hero, one lime-green accent (`~#A8D93A`) reserved for the primary CTA and small icon accents.
- Light equivalent: white base for listing/detail pages, same lime-green accent on primary buttons and active states, hero remains photographic/dark.

**Typography:** clean sans, no unusual treatment — this style's distinctiveness is in layout/decoration, not typography.

**Radius & shadow:** `rounded-2xl` on the search bar and tour/listing cards, `rounded-md` on the small rating badge.

**Signature decoration:** a floating rounded search/filter bar sitting over the lower hero image (Section 25's search widget bar recipe originates here); a decorative dotted orbit circle with a small icon (plane) drifting along it; a subtle grid-line texture over the hero.

**Hero pattern:** Pattern A, paired with the search widget bar rather than a single CTA button.

**Key component notes:** listing cards carry a rating badge overlapping the top corner of the image, title, and a short description — this is the canonical source for that overlap-badge convention.

**Code reference:**
```tsx
<section className="relative h-[70vh] overflow-hidden rounded-3xl">
  <Image src="/hero-mountains.jpg" alt="" fill className="object-cover" />
  <div className="absolute inset-0 bg-gradient-to-t from-black/70 to-black/10" />
  <div className="relative z-10 flex h-full flex-col justify-center gap-4 px-12 text-white">
    <h1 className="max-w-md text-4xl font-semibold">Твое идеальное путешествие</h1>
    <p className="max-w-sm text-sm text-white/60">
      Выбери лучший тур по стране и забронируй его со скидкой 20%.
    </p>
  </div>

  {/* floating search/filter bar over the hero */}
  <div className="absolute inset-x-8 bottom-8 flex flex-col gap-3 rounded-2xl bg-black/60 p-3 backdrop-blur-md sm:flex-row sm:items-center">
    <Select>
      <SelectTrigger className="border-0 bg-transparent text-white"><SelectValue placeholder="Region" /></SelectTrigger>
    </Select>
    <Select>
      <SelectTrigger className="border-0 bg-transparent text-white"><SelectValue placeholder="Date" /></SelectTrigger>
    </Select>
    <Select>
      <SelectTrigger className="border-0 bg-transparent text-white"><SelectValue placeholder="Guests" /></SelectTrigger>
    </Select>
    <Button className="rounded-xl bg-lime-500 text-black hover:bg-lime-400 sm:ml-auto">Найти тур</Button>
  </div>
</section>

{/* listing card with corner rating badge */}
<Card className="overflow-hidden rounded-2xl border-white/10 bg-[#0d1a12] p-0 text-white">
  <div className="relative h-40">
    <Image src="/tour-1.jpg" alt="" fill className="object-cover" />
    <Badge className="absolute right-2 top-2 gap-1 rounded-md bg-black/70 text-white">
      <Star className="h-3 w-3 fill-current" /> 4.9
    </Badge>
  </div>
  <CardContent className="p-4">
    <CardTitle className="text-base text-white">Невероятный Алтай</CardTitle>
    <CardDescription className="mt-1 text-white/50">
      В этом туре вы попробуете свои силы...
    </CardDescription>
  </CardContent>
</Card>
```

---

## 11. Verdant

**Mood:** calm, confident, nature-tech SaaS — the one style that pairs a photographic dark backdrop with light, airy UI chrome.

**When to offer this:** sustainability/climate tech, data/analytics SaaS, any brand wanting to feel "grown, natural, intelligent" rather than sterile-corporate.

**Palette:**
- Native treatment (hybrid): a dark, textured macro-nature photograph (moss/rock) as the page backdrop, with light/glassy UI chrome floating on top — nav pill, cards, and CTA are light or glowing-accent, not dark-matching-the-photo.
- Accent: a single lime-green glow (`~#B9F73E`), used on the CTA, the italic headline word, and the feature-card mini-illustrations.
- Full-light equivalent: white base throughout, same lime-green accent, decorative nature photography reduced to a smaller supporting image rather than the full-page backdrop.

**Typography:** clean rounded sans, with one word of the hero headline set in an italic/script accent color for emphasis ("grows") — the only style in the gallery using a mixed-weight/mixed-style single headline.

**Radius & shadow:** `rounded-full` nav and CTA button, `rounded-2xl` feature cards, soft shadow only on the floating nav pill.

**Signature decoration:** an announcement `Badge` ("New: Verdant 2.0 →") above the headline; a trust-checkmark row under the CTA; three feature cards each pairing an icon-in-bordered-box with a small illustrative graphic (converging data-flow dots, a line chart with a callout tooltip, concentric radar/shield rings); a muted logo cloud row at the bottom.

**Hero pattern:** Pattern B, composed over photography instead of a flat background.

**Key component notes:** this is the reference source for the floating pill nav recipe (Section 26/32) and the "New" announcement badge convention.

**Code reference:**
```tsx
{/* floating pill nav */}
<nav className="mx-auto flex max-w-5xl items-center justify-between rounded-full border bg-background/90 px-6 py-3 backdrop-blur-md">
  <span className="flex items-center gap-2 font-semibold">
    <Leaf className="h-4 w-4 text-lime-500" /> Verdant
  </span>
  <div className="hidden gap-6 text-sm text-muted-foreground sm:flex">
    <a href="#">Product</a><a href="#">Solutions</a><a href="#">Pricing</a>
  </div>
  <div className="flex items-center gap-2">
    <Button variant="ghost" size="sm">Log in</Button>
    <Button size="sm" className="gap-1 rounded-full bg-lime-300 text-black hover:bg-lime-200">
      Get Started <ArrowRight className="h-3 w-3" />
    </Button>
  </div>
</nav>

<section className="relative overflow-hidden py-24 text-center">
  <Image src="/moss-texture.jpg" alt="" fill className="-z-10 object-cover brightness-50" />
  <Badge variant="outline" className="mx-auto w-fit gap-2 rounded-full border-lime-400/40 bg-black/40 text-lime-300">
    <span className="h-1.5 w-1.5 rounded-full bg-lime-400" /> New: Verdant 2.0 is now available
  </Badge>
  <h1 className="mx-auto mt-6 max-w-xl text-5xl font-semibold text-white">
    Intelligence that <span className="italic text-lime-300">grows</span> with you.
  </h1>
  <p className="mx-auto mt-4 max-w-md text-white/60">
    The all-in-one platform for teams who want clarity, speed, and sustainable growth.
  </p>
  <Button className="mt-8 rounded-full bg-lime-300 text-black hover:bg-lime-200">
    Start Free Trial <ArrowRight className="ml-1 h-4 w-4" />
  </Button>
  <div className="mt-4 flex justify-center gap-4 text-xs text-white/50">
    <span className="flex items-center gap-1"><Check className="h-3 w-3" /> No credit card</span>
    <span className="flex items-center gap-1"><Check className="h-3 w-3" /> 14-day free trial</span>
  </div>
</section>

{/* feature card with icon-in-bordered-box + mini illustration */}
<Card className="border-lime-500/10 bg-black/40 p-6 text-white backdrop-blur-md">
  <div className="mb-4 flex h-9 w-9 items-center justify-center rounded-lg border border-lime-400/30 text-lime-300">
    <Leaf className="h-4 w-4" />
  </div>
  <CardTitle className="text-white">Unify your data</CardTitle>
  <CardDescription className="text-white/50">
    Connect all your sources and turn scattered data into a single source of truth.
  </CardDescription>
  <div className="mt-6 h-20 rounded-lg bg-lime-400/5" /> {/* illustration slot */}
</Card>
```

---

## 12. Vortek

**Mood:** stark, minimalist, monochrome product engineering.

**When to offer this:** hardware/product launches, engineering-led brands, anything wanting absolute restraint — this is the strictest zero-color style in the gallery.

**Palette:**
- Pure black-and-white two-tone, **no accent color anywhere** — not even a brand hue. Buttons are solid black or outlined black, full stop. The gallery's own reference shows both a black hero half and a white spec-sheet half of the same product, which is the intended light/dark pairing for this style.

**Typography:** bold modern sans headline with a single hand-drawn-style underline swash beneath one word for emphasis; small inline stat pairs sit directly beside the headline rather than below it.

**Radius & shadow:** `rounded-2xl` on cards and panels, `rounded-lg` on the product-photo container (slightly less rounded, keeping focus on the product), no color in any shadow (pure gray/black shadow only).

**Signature decoration:** annotation/callout label chips connected to a product photo by thin leader lines (the canonical reference for the 3D/product-annotation pattern in Section 29); a floating glass stat panel over the hero image ("Trusted by Clients — 20M+"); testimonial cards that pair the quote with small metric badges (98%, 4.5x, 120+) rather than just a name and role.

**Hero pattern:** a variant of Pattern C, using the inline-stat-beside-headline recipe (Section 25) instead of a stat row below the fold.

**Key component notes:** dual CTA is solid black + outline black (never a colored secondary button); trust logos are grayscale and inline, not a separate row.

**Code reference:**
```tsx
<section className="grid gap-8 bg-white px-8 py-16 sm:grid-cols-2 dark:bg-black">
  <div>
    <p className="text-xs uppercase tracking-widest text-muted-foreground">Futuristic VR</p>
    <h1 className="mt-2 text-6xl font-bold leading-none tracking-tight">
      New <span className="underline decoration-2 underline-offset-4">Future</span><br />Dimension
    </h1>
    <div className="mt-6 flex gap-6 border-l pl-6">
      <div><div className="text-xl font-bold">75%</div><div className="text-xs text-muted-foreground">Growth</div></div>
      <div><div className="text-xl font-bold">99%</div><div className="text-xs text-muted-foreground">Reality</div></div>
    </div>
    <div className="mt-8 flex gap-3">
      <Button className="rounded-full bg-black text-white hover:bg-black/80 dark:bg-white dark:text-black">
        Get Started <ArrowRight className="ml-1 h-4 w-4" />
      </Button>
      <Button variant="outline" className="rounded-full">Watch a Demo</Button>
    </div>
  </div>
  <Image src="/vr-headset.jpg" alt="" width={500} height={500} className="rounded-lg" />
</section>

{/* annotation/callout labels on a product shot */}
<div className="relative">
  <Image src="/product-detail.jpg" alt="" width={600} height={400} className="rounded-lg" />
  {callouts.map((c) => (
    <div key={c.id} className="absolute flex items-center gap-2" style={{ top: c.top, left: c.left }}>
      <span className="h-px w-8 bg-foreground/40" />
      <Badge variant="outline" className="rounded-full border-foreground/20 text-xs">{c.label}</Badge>
    </div>
  ))}
</div>
```

---

## 13. Yoga_spot

**Mood:** soft, calm, restrained wellness.

**When to offer this:** wellness, mindfulness, healthcare-adjacent, or any product wanting the calmest, quietest register in the gallery.

**Palette:**
- Dark (native): deep green-black forest tone, soft white text, **no bright accent color at all** — even more restrained than The_digital_frontier's monochrome, since there isn't even a strong white/black contrast push, just soft mid-tones.
- Light equivalent: white base for non-hero surfaces, the same absence-of-accent discipline (rely on typography and photography, not color, for warmth).

**Typography:** light-weight sans with wide letter-spacing on the eyebrow ("B A L A N C E D  B Y"), a larger single-word title in medium (not bold) weight directly beneath it.

**Radius & shadow:** `rounded-2xl` on video thumbnails, `rounded-full` circular nav arrows, very soft/diffuse shadow throughout — nothing in this style should look sharp.

**Signature decoration:** soft-focus/bokeh mist photography; a horizontal row of video thumbnails each carrying a small circular avatar-overlay in the top-left corner plus a title caption.

**Hero pattern:** a minimal, left-aligned variant of Pattern A — quieter composition than Adventure/Saint_antoinet, with more negative space around the headline.

**Key component notes:** this is the reference source for the soft-focus/bokeh photographic grading rule in Section 27, and for the video-thumbnail-with-avatar-overlay recipe.

**Code reference:**
```tsx
<section className="relative flex h-[80vh] items-center overflow-hidden rounded-3xl">
  <Image src="/hero-mist.jpg" alt="" fill className="object-cover" />
  <div className="absolute inset-0 bg-black/40" />
  <div className="relative z-10 max-w-md px-12 text-white">
    <p className="text-xs uppercase tracking-[0.4em] text-white/60">Balanced By</p>
    <h1 className="mt-2 text-6xl font-medium">Nature</h1>
    <p className="mt-4 text-sm text-white/60">
      Keep your body and mind in equilibrium and be amazed at the full potential of what you can achieve.
    </p>
  </div>
</section>

{/* horizontal video-thumbnail row with avatar overlay */}
<div className="flex gap-4 overflow-x-auto pb-2">
  {videos.map((v) => (
    <div key={v.id} className="relative h-40 w-32 shrink-0 overflow-hidden rounded-2xl">
      <Image src={v.thumb} alt="" fill className="object-cover" />
      <Avatar className="absolute left-2 top-2 h-6 w-6 ring-2 ring-white/80">
        <AvatarImage src={v.avatar} alt="" />
      </Avatar>
      <span className="absolute bottom-2 left-2 text-xs font-medium text-white">{v.title}</span>
    </div>
  ))}
</div>
```

---

## 14. Cross-Style Discipline

- Never blend accent colors, radius scales, or decorative motifs from two different named styles in the same build unless the user has explicitly asked for a mix.
- If the user asks for "a mix of Verdant and Vortek," treat it as a deliberate new composite: state which specific elements are being pulled from each (e.g., "Verdant's floating pill nav and lime accent, Vortek's monochrome product-annotation pattern") before building, so the mix is intentional rather than accidental.
- If no style is specified and the project has no established visual identity yet, ask (Section 0) — do not default to Adventure, Verdant, or any other style as a silent fallback.
- Once a style is chosen, encode it in the project's existing theme tokens/configuration so the implementation itself is the source of truth. Do not create or update `README.md`, `CLAUDE.md`, or another documentation file unless the human explicitly requests that documentation change and the repository rules allow it.
