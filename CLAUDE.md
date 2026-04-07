# CLAUDE.md — Instruktioner för Claude Code

> Denna fil läses automatiskt av Claude Code. Du behöver aldrig förklara dessa regler.

## Vad är det här?

Ett **FHIR-transformeringspaket** från Vårddatahubben (KCHD, SKR).
Paketspecifik information (namn, version, vårdområde) finns i `manifest.json`.

Paketet omvandlar resultatvyer från `vantetid-katarakt` till FHIR R4
MeasureReport-resurser via en C#/Python-pipeline.

## Repostruktur

```
csharp/                C#-pipeline (FHIR-serialisering)
vql/                   VQL-vyer för FHIR-mappning (Denodo)
```

## Branch-modell

- **main** = generisk pipeline. Nya regioner utgår härifrån.
- **region/XXX** = regionspecifik gren (t.ex. region/vgr, region/halland).

### Regler:

- Buggfixar och ny logik görs ALLTID i **main** först, mergeas sedan till regiongrenar.
- Regionspecifika ändringar görs direkt i regionens gren.
- Skapa aldrig en regiongren utan att först fråga användaren.

### Merga uppdateringar till en regiongren:

```bash
git checkout region/vgr
git merge main
git push
```

## Versionshantering — följ ALLTID dessa regler

Vi använder **Semantic Versioning (SemVer)**: `MAJOR.MINOR.PATCH`

### VIKTIGT: Ändra ALDRIG version på eget initiativ

Använd exakt den version som anges i prompten. Om ingen version anges — fråga.

### Typer av ändringar:

- **PATCH** (1.0.0 → 1.0.1): Buggfix. Regionen kan uppgradera utan att ändra något.
- **MINOR** (1.0.x → 1.1.0): Ny funktionalitet tillagd. Bakåtkompatibelt.
- **MAJOR** (1.x.x → 2.0.0): Breaking change. Regionen MÅSTE agera.

### Vid VARJE commit som ändrar funktionalitet:

1. Uppdatera CHANGELOG.md
2. Uppdatera manifest.json (version + release_date)
3. Commit-meddelande: `v1.0.1: Kort beskrivning`
4. Skapa git tag: `git tag v1.0.1`
5. Pusha: `git push && git push --tags`

OBS: Om git push --tags blockeras av proxyn, meddela användaren att taggen
behöver skapas manuellt via GitHub Releases.

## Kvalitetskontroll innan release

- [ ] CHANGELOG.md uppdaterad
- [ ] manifest.json har rätt version och datum
- [ ] Git tag matchar version i manifest.json
- [ ] Tester uppdaterade om ny logik lagts till
- [ ] Regiongrenar mergade från main (om tillämpligt)

## FHIR-kontrakt

FHIR-resurser som genereras av pipelinen läses av nedströms konsumenter.
Resursstruktur och profiler får aldrig ändras utan MAJOR-version.

## Språk

Allt på **svenska**.
