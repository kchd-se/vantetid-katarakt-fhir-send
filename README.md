# Skicka FHIR MeasureReport till Nationella Hubben

C#-kod för att skicka FHIR MeasureReport-bundles från SSIS till Nationella Hubben.

## Filer

| Fil | Vad den gör |
|-----|-------------|
| `ScriptMain.cs` | All C#-kod — klistras in i SSIS Script Task |
| `appsettings.example.json` | Konfigurationsmall — döps om och fylls i (se nedan) |

## Innan du börjar

1. Döp om `appsettings.example.json` till `appsettings.json`
2. Öppna filen och ändra **två** saker:

```json
"ApiKey": "FYLL_I_NYCKEL_HÄR"
```
Byt ut till API-nyckeln ni fått.

```json
"InputBundlePath": "ÄNDRA_TILL_SÖKVÄG_FÖR_FHIR_BUNDLE"
```
Byt ut till sökvägen där er FHIR Bundle JSON-fil ligger på servern, t.ex. `C:\\Data\\FHIR\\fhir_bundle.json`. Använd dubbla backslash (`\\`).

Alla andra värden i filen är redan korrekt ifyllda.

---

## Steg 1: Skapa Script Task i SSIS

1. Öppna ert SSIS-paket (.dtsx) i Visual Studio
2. I **SSIS Toolbox** (vänster sida): dra **Script Task** till **Control Flow**-ytan
3. Dubbelklicka på det nya Script Task:et
4. I dialogrutan som öppnas: klicka **Edit Script...**
5. Visual Studio öppnar nu en kodredigerare (VSTA)

## Steg 2: Lägg till referenser

I kodredigeraren som öppnades i steg 1:

**System.Net.Http:**
1. I **Solution Explorer** (höger sida): högerklicka på **References**
2. Klicka **Add Reference...**
3. Klicka **Assemblies** i vänstermenyn
4. Skriv `System.Net.Http` i sökfältet
5. Kryssa i **System.Net.Http**
6. Klicka **OK**

**Newtonsoft.Json:**
Om ni redan har `Newtonsoft.Json.dll` i ert projekt eller på servern:
1. Högerklicka på **References** igen
2. Klicka **Add Reference...**
3. Klicka **Browse** i vänstermenyn
4. Klicka **Browse...** längst ner
5. Navigera till mappen där `Newtonsoft.Json.dll` ligger
6. Markera filen och klicka **Add**, sedan **OK**

Om ni inte har `Newtonsoft.Json.dll`: ladda ner den från https://www.nuget.org/packages/Newtonsoft.Json — klicka "Download package", byt filändelse från `.nupkg` till `.zip`, extrahera, och hitta DLL:en under `lib/net45/Newtonsoft.Json.dll`.

## Steg 3: Kopiera in koden

I samma kodredigerare:

1. Dubbelklicka på **ScriptMain.cs** i Solution Explorer (den finns redan i projektet)
2. Markera allt befintligt innehåll: **Ctrl+A**
3. Ta bort det: **Delete**
4. Öppna filen `ScriptMain.cs` från detta repo (ladda ner den eller öppna den på GitHub)
5. Markera allt: **Ctrl+A**
6. Kopiera: **Ctrl+C**
7. Gå tillbaka till VSTA-editorn
8. Klistra in: **Ctrl+V**
9. Spara: **Ctrl+S**
10. Stäng VSTA-editorn (krysset eller File → Close)

## Steg 4: Placera appsettings.json

Kopiera `appsettings.json` (den ni döpte om och fyllde i) till **samma katalog** som ert SSIS-paket (.dtsx-filen) ligger i.

## Steg 5: Testkör

1. Högerklicka på SSIS-paketet i Visual Studio → **Execute Package**
2. Script Task:et blir **grönt** = det fungerade
3. Script Task:et blir **rött** = något gick fel — klicka på **Progress**-fliken för att läsa felmeddelandet

Felmeddelanden är på svenska och beskriver vad som är fel och vad du ska göra.
