---
name: hindi-translation
description: Standard for natural, non-literal Hindi (and other-language) translation quality in this project. Use whenever writing or reviewing TranslationAgent's system prompt, or any future translation-related agent/prompt in PositiveNews.
---

# hindi-translation

Reference standard for translation quality, distilled from a "Natural Hindi Translation
Skill" brief Ratnesh pasted after the first Hindi translation shipped and read as
machine-translated / unnatural. This is dev-time reference material for whoever (or
whichever Claude Code session) next touches translation-related agent code — per
`CLAUDE.md`'s "two agent notions kept distinct," this skill file has **no effect on the
live app by itself**. The actual fix lives in `TranslationAgent.cs`'s `SystemPrompt`
constant, which is what's actually sent to Claude on every "Translate" click. When
revising that prompt (or building a translator for another language), pull principles
from here.

## The core failure mode

Literal, word-for-word translation. It's grammatically valid but reads as obviously
translated — a fluent Hindi speaker can tell in one sentence. The fix is not "translate
more carefully," it's "translate for meaning and let sentence structure change freely."

## Golden rule (use this as the actual quality bar)

> Would a native speaker who has never seen the English original believe this was
> originally written in Hindi? If there's any hesitation, it's not natural yet.

## Principles, in priority order

1. **Accuracy first, but never at the cost of naturalness.** Don't drop facts, numbers,
   names, or nuance — but don't preserve English *sentence shape* to protect accuracy.
   Restructure freely; meaning is the unit of translation, not the word or the clause.
2. **Natural over literal.** Prefer how a Hindi speaker would actually say the same
   thing over a dictionary-accurate rendering of each word. Idioms/metaphors get a
   natural Hindi equivalent (or are dropped/reworded if none exists) — never translated
   literally.
3. **Everyday vocabulary over heavy Sanskritized/formal-register words**, unless the
   source itself is formal/technical. Keep common English loanwords Hindi speakers
   actually use in daily speech (e.g. "इंटरनेट," "स्कूल," "टीम," "मोबाइल") rather than
   forcing an obscure pure-Hindi coinage nobody uses.
4. **Match tone and register to the source.** A casual, upbeat positive-news story stays
   casual and upbeat in Hindi — don't default to formal/news-anchor register just
   because it's a news article, unless the English original is itself formal.
5. **Grammar must be genuinely correct Hindi grammar** — gender agreement, postpositions,
   verb agreement, word order (Hindi is SOV, not SVO) — not English grammar with Hindi
   words substituted in.
6. **Preserve exactly, never translate or alter:** proper nouns without a natural Hindi
   form, numbers, dates, technical identifiers/units. Getting a number or name wrong is
   worse than any naturalness issue.
7. **Resolve ambiguity using context**, not the most common dictionary sense of a word —
   pick the sense that fits this specific sentence.
8. **Formatting/structure of the source is preserved** (paragraph breaks, emphasis
   intent) — this is translation, not summarization or rewriting; don't add commentary,
   omit content, or pad it out.
9. **Self-check before finalizing:** read the Hindi in isolation — does it flow like
   something originally written in Hindi, or does the English original show through in
   the sentence structure? If the latter, revise.

## Applying this to `TranslationAgent`

The runtime system prompt (`PositiveNews.Agents/Agents/TranslationAgent.cs`) is
deliberately **not** a verbatim dump of this file — it's sent on every API call (on the
cheap model, per [[model_cost_consciousness]] in memory), so it distills these
principles into the highest-leverage instructions rather than the full rationale above.
Keep that prompt concise; keep this file as the fuller reference for *why* those
instructions exist and for extending to other target languages later.
