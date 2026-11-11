using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mauve.Governance.Requirements;
using Mauve.Governance.Traceability;

namespace Mauve.Governance.Tool;

internal sealed record OwnedFile(string Path, string Sha256);
internal sealed record OwnershipManifest(int SchemaVersion, string BundleVersion, OwnedFile[] Files, string? Profile = null);

[JsonSerializable(typeof(OwnershipManifest))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext;

internal static class RepositorySetup
{
    private const string ManifestPath = ".mauve-governance/manifest.json";
    private static readonly string[] RetiredPaths =
    [
        ".mauve-governance/update.sh",
        "docs/governance/GOVERNANCE_POLICY.md",
        "docs/governance/README.md"
    ];
    internal const string CurrentBundleVersion = "0.13.0";
    private const string CoreProfile = "core";
    private const string RequirementsProfile = "core-requirements";
    private static readonly ManifestJsonContext ReadableManifestContext =
        new(new JsonSerializerOptions { WriteIndented = true, IndentSize = 2 });

    private static readonly IReadOnlyDictionary<string, string> PreviousBundleHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".github/agents/governance-evidence.agent.md"] = "4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F",
            [".github/skills/governance-test-evidence/SKILL.md"] = "273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885",
            ["docs/governance/README.md"] = "59097F2FDFF71F8F4F07A98F4F71D9DF06F14E8DB1D62F3479E711AB9D063E62"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousCoreHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "C850B8B088385410200621ADA1F8E183404075F656B7365FABD361A14939CE74",
            [".github/agents/governance-evidence.agent.md"] = "4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F",
            [".github/skills/governance-test-evidence/SKILL.md"] = "273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            ["docs/governance/README.md"] = "38F61F8CF2BA1C8CAD2BB0D0A5C1D7164F66A97D990722D1350A51BD74ED372D"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousUpdateHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "387CCB1D7C330DD364C91644C1C2C703067A4B3591B7702C58BBC168826FA779",
            [".github/agents/governance-evidence.agent.md"] = "4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F",
            [".github/skills/governance-test-evidence/SKILL.md"] = "273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "6DF53213EAA26DB02048AE57C7D3E4B9C7B8A37DCF313B9A948F240C8B80F36A",
            [".mauve-governance/update.sh"] = "E042D0B2D844C42148A0BCF117EE84536C184A05426AAEB02F1D44DC33684E66",
            ["docs/governance/README.md"] = "98EA88D2F99A3D91507EC137CC7FB758A12480DD7D83263F186F4F21C7986208"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousPolicyHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "E650D60D3A0FF416FA65FC8F74845C91B924795808DE2748A40DDAE7B3FC7FC6",
            [".github/agents/governance-evidence.agent.md"] = "4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F",
            [".github/skills/governance-test-evidence/SKILL.md"] = "273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "6DF53213EAA26DB02048AE57C7D3E4B9C7B8A37DCF313B9A948F240C8B80F36A",
            [".mauve-governance/update.sh"] = "E042D0B2D844C42148A0BCF117EE84536C184A05426AAEB02F1D44DC33684E66",
            ["docs/governance/GOVERNANCE_POLICY.md"] = "CC82623997F357CC15B725A2A9363FAFEB86DD66A8A68AF670B83B3511613B6C",
            ["docs/governance/README.md"] = "1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousSkillsHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "ECF736FDD76A2A635E0177FDA337273E24B8392D9896D7539EDA71B95D30DDC1",
            [".github/agents/governance-evidence.agent.md"] = "4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F",
            [".github/agents/governance-maintainer.agent.md"] = "938F2830CC7B94DA3E1A7DA17EA3F83E80CFBCF87B67AAE1D072A9947A51E92C",
            [".github/skills/governance-analyzer-remediation/SKILL.md"] = "AE036E71092B9F7CCC2604A8C97C1BC0DF753F4006FE7F934D40E2633D4A604F",
            [".github/skills/governance-bootstrap/SKILL.md"] = "C0952BBC16ADCBB3F39A744A7282519B3F7397610CF5CD6CDF9737CD1AAF4830",
            [".github/skills/governance-test-evidence/SKILL.md"] = "273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "6DF53213EAA26DB02048AE57C7D3E4B9C7B8A37DCF313B9A948F240C8B80F36A",
            [".mauve-governance/update.sh"] = "DEF0FF607B819E0527F2E72E69E258D38AFC3F28194E2AB456E69016234722D1",
            ["docs/governance/GOVERNANCE_POLICY.md"] = "CC82623997F357CC15B725A2A9363FAFEB86DD66A8A68AF670B83B3511613B6C",
            ["docs/governance/README.md"] = "1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousReviewHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "4ACB96C5D0524A8E7BCBCBE2B110A17643C7BAF75C50D4ECF0449E940B1CB077",
            [".github/agents/governance-evidence.agent.md"] = "4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F",
            [".github/agents/governance-maintainer.agent.md"] = "938F2830CC7B94DA3E1A7DA17EA3F83E80CFBCF87B67AAE1D072A9947A51E92C",
            [".github/skills/governance-analyzer-remediation/SKILL.md"] = "AE036E71092B9F7CCC2604A8C97C1BC0DF753F4006FE7F934D40E2633D4A604F",
            [".github/skills/governance-bootstrap/SKILL.md"] = "C0952BBC16ADCBB3F39A744A7282519B3F7397610CF5CD6CDF9737CD1AAF4830",
            [".github/skills/governance-test-evidence/SKILL.md"] = "273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "63CCD50EF99985007C2F0AE860ECFBEFE5B572038C74C1944B14DEDD7CEB8FD5",
            [".mauve-governance/update.sh"] = "DEF0FF607B819E0527F2E72E69E258D38AFC3F28194E2AB456E69016234722D1",
            ["docs/governance/GOVERNANCE_POLICY.md"] = "CC82623997F357CC15B725A2A9363FAFEB86DD66A8A68AF670B83B3511613B6C",
            ["docs/governance/README.md"] = "1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousDocumentationHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "BAF11A14486C146E7849405F12EEAF512F8B6E98002AC6585A72E24FBBACFA75",
            [".github/agents/governance-evidence.agent.md"] = "56B9945696C5EC0C5D892C66FE8B5140D86F7C71D95589CC03C0717A99E3BC2C",
            [".github/agents/governance-maintainer.agent.md"] = "2500F9499CBCF6DEF25D31EC0C6FB0D6D2F2401CEC775CAB1C87008EB0F16BD7",
            [".github/skills/governance-analyzer-remediation/SKILL.md"] = "70FEA02AD613218B23A7ED9BB90F36A1733BE529CDD503FD5377CE20D7361A4C",
            [".github/skills/governance-bootstrap/SKILL.md"] = "5CBEEB346BB82D9BB7C21AA1BA9466AEABB890B64F456F9026DDA2EBB5DEFE8F",
            [".github/skills/governance-test-evidence/SKILL.md"] = "C4B46DEF1D12C6DC00631F870F80A9697560F58D297355BCE5C4B42EA7D55162",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "65952EE0C3490BE1D1E892B1A9C6A54488BCE9EFB23B6BCCDC585ED098861760",
            [".mauve-governance/update.sh"] = "DEF0FF607B819E0527F2E72E69E258D38AFC3F28194E2AB456E69016234722D1",
            ["docs/governance/GOVERNANCE_POLICY.md"] = "CC82623997F357CC15B725A2A9363FAFEB86DD66A8A68AF670B83B3511613B6C",
            ["docs/governance/README.md"] = "1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousRequirementsProfileHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "E95E4FD840FC7E53F83D82E1C386F951C1F453B4241A774A1698DA779191414E",
            [".github/agents/governance-evidence.agent.md"] = "56B9945696C5EC0C5D892C66FE8B5140D86F7C71D95589CC03C0717A99E3BC2C",
            [".github/agents/governance-maintainer.agent.md"] = "2500F9499CBCF6DEF25D31EC0C6FB0D6D2F2401CEC775CAB1C87008EB0F16BD7",
            [".github/skills/governance-analyzer-remediation/SKILL.md"] = "70FEA02AD613218B23A7ED9BB90F36A1733BE529CDD503FD5377CE20D7361A4C",
            [".github/skills/governance-bootstrap/SKILL.md"] = "5CBEEB346BB82D9BB7C21AA1BA9466AEABB890B64F456F9026DDA2EBB5DEFE8F",
            [".github/skills/governance-test-evidence/SKILL.md"] = "C4B46DEF1D12C6DC00631F870F80A9697560F58D297355BCE5C4B42EA7D55162",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "65952EE0C3490BE1D1E892B1A9C6A54488BCE9EFB23B6BCCDC585ED098861760",
            [".mauve-governance/update.sh"] = "DEF0FF607B819E0527F2E72E69E258D38AFC3F28194E2AB456E69016234722D1",
            ["docs/governance/GOVERNANCE_POLICY.md"] = "CC82623997F357CC15B725A2A9363FAFEB86DD66A8A68AF670B83B3511613B6C",
            ["docs/governance/README.md"] = "1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousTemplateBundleHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "45449F74B352F0B8049CCD12F26818DF84825ED18BF3C7D64ECF5792799A7805",
            [".github/agents/governance-evidence.agent.md"] = "56B9945696C5EC0C5D892C66FE8B5140D86F7C71D95589CC03C0717A99E3BC2C",
            [".github/agents/governance-maintainer.agent.md"] = "2500F9499CBCF6DEF25D31EC0C6FB0D6D2F2401CEC775CAB1C87008EB0F16BD7",
            [".github/skills/governance-analyzer-remediation/SKILL.md"] = "70FEA02AD613218B23A7ED9BB90F36A1733BE529CDD503FD5377CE20D7361A4C",
            [".github/skills/governance-bootstrap/SKILL.md"] = "8C46E8E2BE90E060A939E35241301410E6244DCBC2168EC8989E34C3BBA321BB",
            [".github/skills/governance-test-evidence/SKILL.md"] = "C4B46DEF1D12C6DC00631F870F80A9697560F58D297355BCE5C4B42EA7D55162",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "65952EE0C3490BE1D1E892B1A9C6A54488BCE9EFB23B6BCCDC585ED098861760",
            [".mauve-governance/update.sh"] = "DEF0FF607B819E0527F2E72E69E258D38AFC3F28194E2AB456E69016234722D1",
            ["docs/governance/GOVERNANCE_POLICY.md"] = "CD5333E5317F99ADFEEAA1D5D3F35BB8154231EA8A40FEEC7A68C5BFC8C41162",
            ["docs/governance/README.md"] = "1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousTraceabilityHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "639C8D3258852B711EA82921264998937EDA70A9BBF31F414CF4C188F05D7AF8",
            [".editorconfig"] = "F24D58B61F197FC8168F50EB3B8FEDADF897188FF940097AD4CC1E7C6F42BD0E",
            [".github/agents/governance-evidence.agent.md"] = "D82AEE7CED84D47E6CAEB172B52862C4FA9156AFCD604D6E2CAE076EAE7E320F",
            [".github/agents/governance-maintainer.agent.md"] = "695A2387DEFF42CC573D9503DA23143E14AC08F4AD50D3EA7E42BC4115A820B5",
            [".github/skills/governance-analyzer-remediation/SKILL.md"] = "954A0E4FC1AFBBA30849E9F48E65A8318EAD60C114122C8BBA16AEBE98D43636",
            [".github/skills/governance-bootstrap/SKILL.md"] = "E7DD9E73DFD617C6BD436FC1940EB12D5A9BD4A0726CFE498AA0050F21D5D05B",
            [".github/skills/governance-test-evidence/SKILL.md"] = "FA2FAB8C577AF27493E58B1C54C6398C482AB3A8F741257C5374BBD53697B79A",
            [".github/workflows/governance-policy.yml"] = "AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A",
            [".github/workflows/governance-update.yml"] = "36EC409CF100831F1EB754F37B8748CAF3EBC1C059ECF2F19CAA14CA652E7C19",
            ["AGENTS.md"] = "0248BE1998E8C93FCBC319EBB5B64BFEBB6020770F03E04CF26AAADC385B2AEE",
            ["docs/Policies/README.md"] = "165F92963F74FBD79610F7AB587F63166D75683F5A8AC35385E79A624E10178B",
            ["docs/governance.md"] = "6D1090B3CB4AB51383318DD81460C6BE134811A4DBB7843B96F11B0EF649EB56",
            ["governance.md"] = "BA6E88DFA91B203FFA8570F961AFF52BEF8E7096C951259B655B149753F884B5",
            ["style-guide.md"] = "15838FAE1FF360435C952299EFB8DC5AA742B763EF9F04F650932F6F6A4EA0A2"
        };

    private static readonly IReadOnlyDictionary<string, string> PreviousDeclarationHashes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".config/dotnet-tools.json"] = "B8DE2E76AE148EB55AA7E89114FB70AACB81BF6E1B45775ECB7588B298C31691",
            [".editorconfig"] = "F24D58B61F197FC8168F50EB3B8FEDADF897188FF940097AD4CC1E7C6F42BD0E",
            [".github/agents/governance-evidence.agent.md"] = "D82AEE7CED84D47E6CAEB172B52862C4FA9156AFCD604D6E2CAE076EAE7E320F",
            [".github/agents/governance-maintainer.agent.md"] = "695A2387DEFF42CC573D9503DA23143E14AC08F4AD50D3EA7E42BC4115A820B5",
            [".github/skills/governance-analyzer-remediation/SKILL.md"] = "954A0E4FC1AFBBA30849E9F48E65A8318EAD60C114122C8BBA16AEBE98D43636",
            [".github/skills/governance-bootstrap/SKILL.md"] = "E7DD9E73DFD617C6BD436FC1940EB12D5A9BD4A0726CFE498AA0050F21D5D05B",
            [".github/skills/governance-test-evidence/SKILL.md"] = "FA2FAB8C577AF27493E58B1C54C6398C482AB3A8F741257C5374BBD53697B79A",
            [".github/workflows/governance-policy.yml"] = "4F72AACEAEF3FD1536458B518CC65E2A665A3D5963589EFC9CF2CA55EB6A2258",
            [".github/workflows/governance-update.yml"] = "36EC409CF100831F1EB754F37B8748CAF3EBC1C059ECF2F19CAA14CA652E7C19",
            ["AGENTS.md"] = "0248BE1998E8C93FCBC319EBB5B64BFEBB6020770F03E04CF26AAADC385B2AEE",
            ["docs/Policies/README.md"] = "165F92963F74FBD79610F7AB587F63166D75683F5A8AC35385E79A624E10178B",
            ["docs/governance.md"] = "8DC2D520CD88E6244399E12B068835E276149F0351FED8C7B5EE50BC60A6BB39",
            ["governance.md"] = "54E802496C0E30C36DB0CED3BBDC1BDE008F72CC8560AD189641EF1C14131A4C",
            ["style-guide.md"] = "15838FAE1FF360435C952299EFB8DC5AA742B763EF9F04F650932F6F6A4EA0A2"
        };

    private static readonly string[] TemplatePaths =
    [
        ".config/dotnet-tools.json",
        ".editorconfig",
        ".github/agents/governance-evidence.agent.md",
        ".github/agents/governance-maintainer.agent.md",
        ".github/skills/governance-analyzer-remediation/SKILL.md",
        ".github/skills/governance-bootstrap/SKILL.md",
        ".github/skills/governance-test-evidence/SKILL.md",
        ".github/workflows/governance-policy.yml",
        ".github/workflows/governance-update.yml",
        "AGENTS.md",
        "docs/governance.md",
        "docs/Policies/README.md",
        "governance.md",
        "style-guide.md"
    ];

    private static readonly string[] ScaffoldPaths =
    [
        "docs/Requirements/README.md",
        "docs/Requirements/Planned/README.md",
        "docs/validation-gaps.md"
    ];

    private static readonly IReadOnlyDictionary<string, string> AllTemplates = LoadTemplates();
    private static readonly IReadOnlyDictionary<string, string> Templates = AllTemplates
        .Where(item => !ScaffoldPaths.Contains(item.Key, StringComparer.Ordinal))
        .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);

    internal static IReadOnlyList<string> EditableScaffoldPaths => ScaffoldPaths;

    public static int Run(string command, string root, bool check,
        string? profile = null, string? bundleVersion = null)
    {
        if (profile is not null && !SupportedProfile(profile))
        {
            throw new InvalidDataException($"Unsupported Governance profile '{profile}'.");
        }
        if (bundleVersion is not null && bundleVersion != CurrentBundleVersion)
        {
            throw new InvalidDataException($"Unsupported Governance bundle version '{bundleVersion}'.");
        }
        if (command == "init" && (profile is null || bundleVersion is null))
        {
            throw new InvalidDataException("Enrollment requires an explicit --profile and --bundle-version.");
        }

        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Repository root does not exist: '{fullRoot}'.");
        }

        var manifestFile = Resolve(fullRoot, ManifestPath);
        var existing = File.Exists(manifestFile) ? Load(manifestFile) : null;
        if (existing?.SchemaVersion == 1 &&
            (existing.BundleVersion != "0.2.0" ||
             existing.Files.Length != PreviousBundleHashes.Count ||
             existing.Files.Any(file =>
                 !PreviousBundleHashes.TryGetValue(file.Path, out var hash) || file.Sha256 != hash)))
        {
            throw new InvalidDataException("Legacy Governance bundle is unknown or its ownership manifest was modified.");
        }
        if (existing is { SchemaVersion: 2 } && existing.BundleVersion != CurrentBundleVersion)
        {
            var expected = existing.BundleVersion switch
            {
                "0.3.0" => PreviousCoreHashes,
                "0.4.0" => PreviousUpdateHashes,
                "0.5.0" => PreviousPolicyHashes,
                "0.6.0" => PreviousSkillsHashes,
                "0.7.0" => PreviousReviewHashes,
                "0.8.0" => PreviousDocumentationHashes,
                "0.9.0" => PreviousRequirementsProfileHashes,
                "0.10.0" => PreviousTemplateBundleHashes,
                "0.11.0" => PreviousTraceabilityHashes,
                "0.12.0" => PreviousDeclarationHashes,
                _ => null
            };
            if (existing.BundleVersion is not ("0.9.0" or "0.10.0" or "0.11.0" or "0.12.0") && existing.Profile != CoreProfile ||
                expected is null ||
                existing.Files.Length != expected.Count ||
                existing.Files.Any(file =>
                    !expected.TryGetValue(file.Path, out var hash) || file.Sha256 != hash))
            {
                throw new InvalidDataException("Governance bundle version is unknown or its ownership manifest was modified.");
            }
        }
        if (command == "init" && existing is not null)
        {
            throw new InvalidDataException("Repository is already enrolled; use update.");
        }

        if (command != "init" && existing is null)
        {
            throw new InvalidDataException("Repository has no ownership manifest; use init for explicit enrollment.");
        }

        if (command == "init" && profile == RequirementsProfile)
        {
            if (!Directory.Exists(Path.Combine(fullRoot, "docs", "Requirements")))
            {
                throw new InvalidDataException("Profile core-requirements requires docs/Requirements and its Planned lifecycle.");
            }
            RequirementDocuments.Validate(fullRoot);
        }

        var owned = existing?.Files.ToDictionary(static file => file.Path, StringComparer.Ordinal) ??
            new Dictionary<string, OwnedFile>(StringComparer.Ordinal);
        if (owned.Keys.Any(path => !Templates.ContainsKey(path) &&
            !RetiredPaths.Contains(path, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Ownership manifest contains paths from an unsupported bundle; manual migration is required.");
        }
        if (existing?.BundleVersion == CurrentBundleVersion &&
            RetiredPaths.Any(owned.ContainsKey))
        {
            throw new InvalidDataException("Current Governance bundle cannot own retired paths.");
        }
        if (existing?.BundleVersion == CurrentBundleVersion &&
            (owned.Count != Templates.Count || Templates.Any(item =>
                !owned.TryGetValue(item.Key, out var file) || file.Sha256 != Hash(item.Value))))
        {
            throw new InvalidDataException("Ownership manifest hashes do not match the installed bundle.");
        }
        if (existing is not null && existing.BundleVersion != CurrentBundleVersion)
        {
            if (!Version.TryParse(existing.BundleVersion, out var installed) ||
                !Version.TryParse(CurrentBundleVersion, out var current))
            {
                throw new InvalidDataException("Governance bundle version is invalid.");
            }

            if (installed > current)
            {
                throw new InvalidDataException("Downgrading a Governance bundle is not supported.");
            }
        }

        var retiredFiles = new List<string>();
        foreach (var path in RetiredPaths)
        {
            if (!owned.TryGetValue(path, out var retired))
            {
                continue;
            }
            var destination = Resolve(fullRoot, path);
            if (Directory.Exists(destination))
            {
                throw new InvalidDataException($"Retired managed path '{path}' is a directory.");
            }
            if (File.Exists(destination))
            {
                if (new FileInfo(destination).Length > 1024 * 1024 ||
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destination))) != retired.Sha256)
                {
                    throw new InvalidDataException($"Retired managed path '{path}' was modified; resolve it before migration.");
                }
                retiredFiles.Add(destination);
            }
        }

        var changes = new List<(string Path, string Content)>();
        foreach (var (path, content) in Templates)
        {
            var destination = Resolve(fullRoot, path);
            if (Directory.Exists(destination))
            {
                throw new InvalidDataException($"Managed path '{path}' is a directory.");
            }
            if (File.Exists(destination) && !owned.ContainsKey(path))
            {
                throw new InvalidDataException($"Unmanaged file at '{path}'; explicit adoption is required.");
            }

            if (!File.Exists(destination))
            {
                changes.Add((destination, content));
            }
            else
            {
                if (new FileInfo(destination).Length > 1024 * 1024)
                {
                    throw new InvalidDataException($"Managed file '{path}' is too large.");
                }
                if (File.ReadAllText(destination) != content)
                {
                    changes.Add((destination, content));
                }
            }
        }

        var scaffoldChanges = new List<(string Path, string Content)>();
        if (command != "validate")
        {
            foreach (var path in ScaffoldPaths)
            {
                var destination = Resolve(fullRoot, path);
                if (Directory.Exists(destination))
                {
                    throw new InvalidDataException($"Scaffold path '{path}' is a directory.");
                }
                if (!File.Exists(destination))
                {
                    scaffoldChanges.Add((destination, AllTemplates[path]));
                }
            }
        }

        var selectedProfile = existing?.Profile ?? profile ?? CoreProfile;
        var manifest = new OwnershipManifest(2, CurrentBundleVersion, Templates
            .Select(static item => new OwnedFile(item.Key, Hash(item.Value))).ToArray(), selectedProfile);
        var manifestText = JsonSerializer.Serialize(manifest, ReadableManifestContext.OwnershipManifest) + "\n";
        var manifestChanged = !File.Exists(manifestFile) || File.ReadAllText(manifestFile) != manifestText;
        if (command == "validate")
        {
            if (changes.Count != 0 || manifestChanged || retiredFiles.Count != 0)
            {
                throw new InvalidDataException("Managed Governance files differ from the ownership manifest or bundle.");
            }

            if (selectedProfile == RequirementsProfile &&
                !Directory.Exists(Path.Combine(fullRoot, "docs", "Requirements")))
            {
                throw new InvalidDataException("Profile core-requirements requires docs/Requirements and its Planned lifecycle.");
            }
            RequirementDocuments.Validate(fullRoot);
            var traceability = TraceabilityGenerator.GenerateFromDirectory(fullRoot);
            if (!traceability.IsValid)
            {
                var diagnostic = traceability.Diagnostics[0];
                throw new InvalidDataException(
                    $"{diagnostic.Code}: {diagnostic.Path}:{diagnostic.Line}: {diagnostic.Message}");
            }
            ValidationGaps.Validate(fullRoot, traceability.Tests.SelectMany(test => test.Requirements));
            Console.WriteLine($"Managed Governance files, active requirement methods and source declarations are valid ({traceability.Tests.Count} tests).");
            return 0;
        }

        if (check)
        {
            Console.WriteLine($"{changes.Count + retiredFiles.Count} managed files and {scaffoldChanges.Count} editable scaffolds would change; manifest change: {manifestChanged}.");
            return changes.Count == 0 && scaffoldChanges.Count == 0 && retiredFiles.Count == 0 && !manifestChanged ? 0 : 1;
        }

        foreach (var (path, content) in changes)
        {
            WriteAtomic(path, content);
        }

        foreach (var (path, content) in scaffoldChanges)
        {
            WriteAtomic(path, content);
        }

        foreach (var retiredFile in retiredFiles)
        {
            File.Delete(retiredFile);
        }

        if (manifestChanged)
        {
            WriteAtomic(manifestFile, manifestText);
        }

        Console.WriteLine($"Governance {CurrentBundleVersion}: applied {changes.Count + retiredFiles.Count} managed file changes and {scaffoldChanges.Count} editable scaffolds.");
        return 0;
    }

    private static OwnershipManifest Load(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024)
        {
            throw new InvalidDataException("Governance ownership manifest is too large.");
        }

        using var stream = File.OpenRead(path);
        using (var document = JsonDocument.Parse(stream))
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(root, ["SchemaVersion", "BundleVersion", "Files", "Profile"]) ||
                !root.TryGetProperty("Files", out var files) ||
                files.ValueKind != JsonValueKind.Array ||
                files.EnumerateArray().Any(file => file.ValueKind != JsonValueKind.Object ||
                    !HasOnlyProperties(file, ["Path", "Sha256"])))
            {
                throw new InvalidDataException("Governance ownership manifest has unknown or malformed fields.");
            }
        }
        stream.Position = 0;
        var manifest = JsonSerializer.Deserialize(stream, ManifestJsonContext.Default.OwnershipManifest)
            ?? throw new InvalidDataException("Governance ownership manifest is empty.");
        if ((manifest.SchemaVersion != 1 && manifest.SchemaVersion != 2) ||
            manifest.SchemaVersion == 2 && (manifest.Profile is null || !SupportedProfile(manifest.Profile)) ||
            string.IsNullOrWhiteSpace(manifest.BundleVersion) ||
            manifest.Files is null || manifest.Files.Length > 1000 ||
            manifest.Files.Any(static item => item is null || item.Path is null ||
                item.Sha256 is not { Length: 64 } || !item.Sha256.All(Uri.IsHexDigit)) ||
            manifest.Files.Select(static item => item.Path).Distinct(StringComparer.Ordinal).Count() != manifest.Files.Length)
        {
            throw new InvalidDataException("Governance ownership manifest schema is invalid.");
        }

        return manifest;
    }

    internal static string CurrentProfile(string root) =>
        Load(Resolve(Path.GetFullPath(root), ManifestPath)).Profile ?? CoreProfile;

    private static bool SupportedProfile(string profile) =>
        profile is CoreProfile or RequirementsProfile;

    private static bool HasOnlyProperties(JsonElement element, string[] allowed)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return element.EnumerateObject().All(property =>
            allowed.Contains(property.Name, StringComparer.Ordinal) && seen.Add(property.Name));
    }

    private static string Resolve(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) ||
            relative.Split('/').Any(static part => part is "" or "." or ".."))
        {
            throw new InvalidDataException("Governance managed path is unsafe.");
        }

        var current = root;
        foreach (var part in relative.Split('/'))
        {
            current = Path.Combine(current, part);
            if (new FileInfo(current).LinkTarget is not null)
            {
                throw new InvalidDataException("Governance managed path passes through a symlink.");
            }
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Governance managed path passes through a symlink.");
                }
            }
        }

        return current;
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private static IReadOnlyDictionary<string, string> LoadTemplates()
    {
        const string prefix = "GovernanceTemplate/";
        var assembly = typeof(RepositorySetup).Assembly;
        var paths = TemplatePaths.Concat(ScaffoldPaths).ToArray();
        if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
        {
            throw new InvalidDataException("Governance template paths must be unique.");
        }
        var expected = paths.Select(static path => prefix + path).ToHashSet(StringComparer.Ordinal);
        var actual = assembly.GetManifestResourceNames()
            .Where(static name => name.StartsWith(prefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            throw new InvalidDataException("Embedded Governance templates do not match the managed template paths.");
        }

        var templates = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            using var stream = assembly.GetManifestResourceStream(prefix + path)
                ?? throw new InvalidDataException($"Managed Governance template '{path}' is missing.");
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = reader.ReadToEnd();
            if (path == ".config/dotnet-tools.json")
            {
                const string versionPlaceholder = "{{CURRENT_BUNDLE_VERSION}}";
                if (!content.Contains(versionPlaceholder, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Tool manifest template has no bundle version placeholder.");
                }
                content = content.Replace(versionPlaceholder, CurrentBundleVersion, StringComparison.Ordinal);
            }
            templates.Add(path, content);
        }
        return templates;
    }

    private static void WriteAtomic(string destination, string content)
    {
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".governance-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
