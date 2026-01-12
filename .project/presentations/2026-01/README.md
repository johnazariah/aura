# Aura Presentation - January 2026

**Presentation Date:** Thursday, January 15, 2026  
**Duration:** ~45 minutes (including Q&A)

## Contents

| File | Purpose |
|------|---------|
| [slides.md](slides.md) | Slide deck in Markdown (convert to PPT via Marp/Pandoc) |
| [handout.md](handout.md) | Printed handout for attendees |
| [speaker-notes.md](speaker-notes.md) | Detailed speaker notes per slide |
| [diagrams/](diagrams/) | Architecture diagrams |

## Presentation Structure

1. **Motivations** (5 min) - Why local-first AI matters
2. **Top-Level Architecture** (10 min) - System overview
3. **Core Use Cases** (15 min) - Developer workflows demo
4. **Technical Implementation** (10 min) - Code walkthrough
5. **Q&A** (5 min)

## Converting to PowerPoint

### Option 1: Marp (Recommended)

```bash
# Install Marp CLI
npm install -g @marp-team/marp-cli

# Convert to PPTX
marp slides.md --pptx -o aura-presentation.pptx

# Convert to PDF (for handouts)
marp handout.md --pdf -o aura-handout.pdf
```

### Option 2: Pandoc

```bash
pandoc slides.md -o aura-presentation.pptx
```

## Printing Handouts

The handout is designed for **2-up printing** (2 pages per sheet, double-sided).
