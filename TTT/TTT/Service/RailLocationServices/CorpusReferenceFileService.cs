using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using TTT.DataSets.Options;

namespace TTT.Service.RailLocationServices;

public sealed class CorpusReferenceFileService(IOptions<NetRailOptions> netRailOptions,
    IWebHostEnvironment env,
    ILogger<CorpusReferenceFileService> log)
{

    /// <summary>
    /// Downloads CORPUS (gz) and extracts JSON into the project's Data/ folder.
    /// Returns the full path to the extracted JSON file.
    /// </summary>
    public async Task<string> DownloadAndExtractCorpusAsync(CancellationToken ct)
    {
        var railOptions = netRailOptions.Value;

        if (string.IsNullOrWhiteSpace(railOptions.Username) || string.IsNullOrWhiteSpace(railOptions.Password))
            throw new InvalidOperationException("NetRailOptions Username/Password not configured.");

        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);

        var gzPath = Path.Combine(dataDir, "CORPUSExtract.json.gz");
        var jsonPath = Path.Combine(dataDir, "CORPUSExtract.json");

        using (var client = new HttpClient())
        {
            string auth = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{railOptions.Username}:{railOptions.Password}"));
            
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

            try
            {
                var response = await client.GetAsync(railOptions.CorpusUrl, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    throw new HttpRequestException(
                        $"CORPUS download failed. Status={(int)response.StatusCode} {response.ReasonPhrase}. Body={body}");
                }
                
                // Save gz to disk
                await using (var remoteStream = await response.Content.ReadAsStreamAsync(ct))
                await using (var file = File.Create(gzPath))
                {
                    await remoteStream.CopyToAsync(file, ct);
                }

                // Determine if it's actually gz (usually is)
                if (IsGzipFile(gzPath))
                {
                    log.LogInformation("Extracting CORPUS JSON to {Path}", jsonPath);

                    await using var gzFile = File.OpenRead(gzPath);
                    await using var gzip = new GZipStream(gzFile, CompressionMode.Decompress);
                    await using var jsonFile = File.Create(jsonPath);

                    await gzip.CopyToAsync(jsonFile, ct);
                }
                else
                {
                    // If server ever returns plain JSON, just copy it
                    log.LogWarning("Downloaded CORPUS file did not look like gzip. Copying as JSON.");
                    File.Copy(gzPath, jsonPath, overwrite: true);
                }

                log.LogInformation("CORPUS ready: {JsonPath}", jsonPath);
                return jsonPath;
            }
            catch (HttpRequestException e)
            {
                log.LogError(e, "CORPUS download failed");
                return e.Message;
            }
        }
    }

    private static bool IsGzipFile(string path)
    {
        // gzip magic bytes: 1F 8B
        Span<byte> header = stackalloc byte[2];
        using var fs = File.OpenRead(path);
        
        if (fs.Read(header) != 2) 
            return false;
        
        return header[0] == 0x1F && header[1] == 0x8B;
    }
}
