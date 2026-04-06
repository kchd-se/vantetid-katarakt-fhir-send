/*
 * FHIR MeasureReport-sändning till Nationella Hubben
 *
 * Denna fil innehåller all kod som behövs för att skicka en FHIR Bundle
 * till Nationella Hubbens MeasureReport-endpoint.
 *
 * Konfiguration läses från appsettings.json som ska ligga i samma katalog
 * som SSIS-paketet (.dtsx).
 */

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Dts.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


[Microsoft.SqlServer.Dts.Tasks.ScriptTask.SSISScriptTaskEntryPointAttribute]
public partial class ScriptMain : Microsoft.SqlServer.Dts.Tasks.ScriptTask.VSTARTScriptObjectModelBase
{
    public void Main()
    {
        bool fireAgain = false;

        try
        {
            string packagePath = Dts.Variables["System::PackageDirectory"].Value.ToString();
            string settingsPath = Path.Combine(packagePath, "appsettings.json");

            var sender = new FhirBundleSender(
                settingsPath,
                msg => Dts.Events.FireInformation(0, "FhirBundleSender", msg, "", 0, ref fireAgain),
                msg => Dts.Events.FireError(0, "FhirBundleSender", msg, "", 0)
            );

            bool success = sender.Send();

            Dts.TaskResult = success
                ? (int)ScriptResults.Success
                : (int)ScriptResults.Failure;
        }
        catch (Exception ex)
        {
            Dts.Events.FireError(0, "FhirBundleSender",
                $"Oväntat fel: {ex.Message}", "", 0);
            Dts.TaskResult = (int)ScriptResults.Failure;
        }
    }

    enum ScriptResults
    {
        Success = DTSExecResult.Success,
        Failure = DTSExecResult.Failure
    }
}


public class AppSettings
{
    public string HubUrl { get; set; }
    public string ApiEndpoint { get; set; }
    public string ApiKey { get; set; }
    public string SourceRegion { get; set; }
    public string SourceRegionName { get; set; }
    public string SourceRegionId { get; set; }
    public string InputBundlePath { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
    public string Encoding { get; set; } = "utf-8";
}


public class FhirBundleSender
{
    private readonly AppSettings _settings;
    private readonly Action<string> _logInfo;
    private readonly Action<string> _logError;

    public FhirBundleSender(string settingsPath, Action<string> logInfo, Action<string> logError)
    {
        _logInfo = logInfo;
        _logError = logError;
        _settings = LoadSettings(settingsPath);
    }

    private AppSettings LoadSettings(string settingsPath)
    {
        _logInfo($"Läser konfiguration från: {settingsPath}");

        if (!File.Exists(settingsPath))
        {
            throw new FileNotFoundException(
                $"FEL: Konfigurationsfilen hittades inte: {settingsPath}\n" +
                "     Kontrollera att appsettings.json ligger i samma katalog som SSIS-paketet.");
        }

        string json = File.ReadAllText(settingsPath, System.Text.Encoding.UTF8);
        var settings = JsonConvert.DeserializeObject<AppSettings>(json);

        if (settings == null)
        {
            throw new InvalidOperationException(
                "FEL: appsettings.json kunde inte tolkas.\n" +
                "     Kontrollera att filen innehåller giltig JSON.");
        }

        ValidateSettings(settings);

        _logInfo("Konfiguration laddad:");
        _logInfo($"  HubUrl: {settings.HubUrl}");
        _logInfo($"  ApiEndpoint: {settings.ApiEndpoint}");
        _logInfo($"  SourceRegion: {settings.SourceRegion}");
        _logInfo($"  SourceRegionName: {settings.SourceRegionName}");
        _logInfo($"  InputBundlePath: {settings.InputBundlePath}");
        _logInfo($"  TimeoutSeconds: {settings.TimeoutSeconds}");

        return settings;
    }

    private void ValidateSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.HubUrl))
            throw new InvalidOperationException("FEL: HubUrl saknas i appsettings.json");

        if (string.IsNullOrWhiteSpace(settings.ApiEndpoint))
            throw new InvalidOperationException("FEL: ApiEndpoint saknas i appsettings.json");

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException("FEL: ApiKey saknas i appsettings.json");

        if (string.IsNullOrWhiteSpace(settings.SourceRegion))
            throw new InvalidOperationException("FEL: SourceRegion saknas i appsettings.json");

        if (string.IsNullOrWhiteSpace(settings.SourceRegionName))
            throw new InvalidOperationException("FEL: SourceRegionName saknas i appsettings.json");

        if (string.IsNullOrWhiteSpace(settings.SourceRegionId))
            throw new InvalidOperationException("FEL: SourceRegionId saknas i appsettings.json");

        if (string.IsNullOrWhiteSpace(settings.InputBundlePath))
            throw new InvalidOperationException("FEL: InputBundlePath saknas i appsettings.json");

        if (settings.TimeoutSeconds <= 0)
            throw new InvalidOperationException("FEL: TimeoutSeconds måste vara större än 0");
    }

    private JObject LoadBundle()
    {
        string bundlePath = _settings.InputBundlePath;
        _logInfo($"Läser FHIR Bundle från: {bundlePath}");

        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException(
                $"FEL: Kunde inte läsa filen {bundlePath}\n" +
                "     Kontrollera att sökvägen i appsettings.json (InputBundlePath) stämmer\n" +
                "     och att filen existerar.");
        }

        string json = File.ReadAllText(bundlePath, System.Text.Encoding.UTF8);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                $"FEL: Filen {bundlePath} är tom.\n" +
                "     Kontrollera att filen har skapats korrekt.");
        }

        var bundle = JObject.Parse(json);

        var resourceType = bundle["resourceType"]?.ToString();
        if (resourceType != "Bundle")
        {
            throw new InvalidOperationException(
                $"FEL: Filen innehåller inte en giltig FHIR Bundle.\n" +
                $"     Förväntade resourceType='Bundle', fick '{resourceType}'.");
        }

        var entries = bundle["entry"] as JArray;
        int entryCount = entries?.Count ?? 0;
        _logInfo($"Bundle laddad: {entryCount} entries");

        if (entries != null && entries.Count > 0)
        {
            var firstResource = entries[0]["resource"];
            var periodStart = firstResource?["period"]?["start"]?.ToString();
            var periodEnd = firstResource?["period"]?["end"]?.ToString();
            if (periodStart != null && periodEnd != null)
            {
                _logInfo($"Skickar data för period: {periodStart} till {periodEnd}");
            }
        }

        return bundle;
    }

    private object BuildRequestBody(JObject bundle)
    {
        return new
        {
            bundle = bundle,
            source_region = _settings.SourceRegion,
            metadata = new
            {
                uploaded_by = _settings.SourceRegionName,
                uploaded_by_id = _settings.SourceRegionId
            }
        };
    }

    public async Task<bool> SendAsync()
    {
        try
        {
            var bundle = LoadBundle();
            var requestBody = BuildRequestBody(bundle);
            string json = JsonConvert.SerializeObject(requestBody, Formatting.None);

            string fullUrl = _settings.HubUrl.TrimEnd('/') + _settings.ApiEndpoint;
            _logInfo($"Skickar till: {fullUrl}");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
                {
                    CharSet = "utf-8"
                };

                client.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);

                _logInfo("Skickar request...");
                HttpResponseMessage response;

                try
                {
                    response = await client.PostAsync(fullUrl, content);
                }
                catch (TaskCanceledException)
                {
                    _logError(
                        $"FEL: Hubben svarade inte inom {_settings.TimeoutSeconds} sekunder.\n" +
                        "     Kontrollera att HubUrl i appsettings.json är korrekt\n" +
                        "     och att nätverket fungerar.");
                    return false;
                }
                catch (HttpRequestException ex)
                {
                    _logError(
                        $"FEL: Nätverksfel vid anslutning till hubben.\n" +
                        $"     Detaljer: {ex.Message}\n" +
                        "     Kontrollera att HubUrl i appsettings.json är korrekt\n" +
                        "     och att nätverket fungerar.");
                    return false;
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                _logInfo($"HTTP-status: {(int)response.StatusCode} {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    return HandleSuccessResponse(responseBody);
                }
                else
                {
                    return HandleErrorResponse(response.StatusCode, responseBody);
                }
            }
        }
        catch (FileNotFoundException ex)
        {
            _logError(ex.Message);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logError(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logError(
                $"FEL: Ett oväntat fel uppstod.\n" +
                $"     Typ: {ex.GetType().Name}\n" +
                $"     Meddelande: {ex.Message}");
            return false;
        }
    }

    private bool HandleSuccessResponse(string responseBody)
    {
        try
        {
            var jobj = JObject.Parse(responseBody);

            _logInfo("========================================");
            _logInfo("FRAMGÅNG! Bundle mottagen av hubben.");
            _logInfo("========================================");
            _logInfo($"Status: {jobj["status"]}");
            _logInfo($"Meddelande: {jobj["message"]}");
            _logInfo($"Antal MeasureReports: {jobj["measure_reports_received"]}");
            _logInfo($"Period: {jobj["period"]}");
            _logInfo($"Region: {jobj["source_region"]}");
            _logInfo($"Tidsstämpel: {jobj["timestamp"]}");
            return true;
        }
        catch (JsonException)
        {
            _logInfo("Svar från hubben:");
            _logInfo(responseBody);
            return true;
        }
    }

    private bool HandleErrorResponse(HttpStatusCode statusCode, string responseBody)
    {
        _logError($"FEL: Hubben svarade med HTTP {(int)statusCode}");

        try
        {
            var jobj = JObject.Parse(responseBody);
            var detail = jobj["detail"];

            string message;
            string errorCode;

            if (detail != null && detail.Type == JTokenType.Object)
            {
                message = detail["message"]?.ToString();
                errorCode = detail["error_code"]?.ToString();
            }
            else if (detail != null && detail.Type == JTokenType.String)
            {
                message = detail.ToString();
                errorCode = null;
            }
            else
            {
                message = jobj["message"]?.ToString();
                errorCode = jobj["error_code"]?.ToString();
            }

            if (!string.IsNullOrEmpty(message))
                _logError($"Felmeddelande: {message}");
            if (!string.IsNullOrEmpty(errorCode))
                _logError($"Felkod: {errorCode}");

            switch (statusCode)
            {
                case HttpStatusCode.Unauthorized:
                    _logError("\nÅTGÄRD: Kontrollera att ApiKey i appsettings.json är korrekt.");
                    break;
                case HttpStatusCode.BadRequest:
                    _logError("\nÅTGÄRD: Kontrollera att FHIR Bundle-filen är korrekt formaterad. Se felmeddelandet ovan.");
                    break;
                case HttpStatusCode.InternalServerError:
                    _logError("\nÅTGÄRD: Serverfel. Försök igen om en stund.");
                    break;
            }
        }
        catch (JsonException)
        {
            _logError($"Råsvar: {responseBody}");
        }

        return false;
    }

    public bool Send()
    {
        return SendAsync().GetAwaiter().GetResult();
    }
}
