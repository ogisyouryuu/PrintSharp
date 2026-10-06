import pathlib
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile


package_dir = pathlib.Path(sys.argv[1]).resolve()
version = sys.argv[2]
expected = {
    "PrintSharp": "net10.0",
    "PrintSharp.Excel": "net10.0",
    "PrintSharp.Pdf": "net10.0",
    "PrintSharp.Windows": "net10.0-windows",
}
packages = sorted(package_dir.glob("*.nupkg"))
if len(packages) != len(expected):
    raise SystemExit(f"Expected {len(expected)} packages, found {len(packages)}")

for package in packages:
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        nuspec_name = next(name for name in names if name.endswith(".nuspec"))
        metadata = ET.fromstring(archive.read(nuspec_name)).find("{*}metadata")
        package_id = metadata.findtext("{*}id")
        package_version = metadata.findtext("{*}version")
        if package_id not in expected:
            raise SystemExit(f"Unexpected package ID: {package_id}")
        if package_version != version:
            raise SystemExit(f"{package_id} has version {package_version}, expected {version}")
        if not metadata.findtext("{*}authors"):
            raise SystemExit(f"{package_id} is missing its authors")
        if not metadata.findtext("{*}description"):
            raise SystemExit(f"{package_id} is missing its description")
        if metadata.findtext("{*}license") != "MIT":
            raise SystemExit(f"{package_id} is missing the MIT license expression")
        repository_element = metadata.find("{*}repository")
        repository = repository_element.get("url") if repository_element is not None else None
        if repository != "https://github.com/ogisyouryuu/PrintSharp":
            raise SystemExit(f"{package_id} has an invalid repository URL")
        if "README.md" not in names:
            raise SystemExit(f"{package_id} does not contain README.md")
        if not any(name.startswith(f"lib/{expected[package_id]}") for name in names):
            raise SystemExit(f"{package_id} does not contain its expected target framework")
        if any(".Tests" in name or "Benchmark" in name for name in names):
            raise SystemExit(f"{package_id} contains test or benchmark files")
        if package_id == "PrintSharp.Excel" and not any(
            name.startswith("analyzers/dotnet/cs/") and name.endswith(".dll") for name in names
        ):
            raise SystemExit("PrintSharp.Excel does not contain its source generator analyzer")

with tempfile.TemporaryDirectory(prefix="printsharp-consumer-") as temp:
    project = pathlib.Path(temp)
    references = "\n".join(
        f'    <PackageReference Include="{package_id}" Version="{version}" />'
        for package_id in expected
    )
    (project / "Consumer.csproj").write_text(
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"
        "<OutputType>Exe</OutputType><TargetFramework>net10.0-windows</TargetFramework>"
        "<EnableWindowsTargeting>true</EnableWindowsTargeting><ImplicitUsings>enable</ImplicitUsings>"
        "<Nullable>enable</Nullable></PropertyGroup><ItemGroup>"
        f"{references}</ItemGroup></Project>",
        encoding="utf-8",
    )
    (project / "Program.cs").write_text(
        "using PrintSharp.Documents;\n"
        "using PrintSharp.Fluent;\n"
        "var document = Document.Create(d => d.Title(\"Package smoke test\")"
        ".Page(\"Page 1\", p => p.Columns(100f).Rows(24f).Cell(\"A1\", \"Works\")));\n",
        encoding="utf-8",
    )
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="local", value=str(package_dir))
    ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
    ET.ElementTree(config).write(project / "NuGet.Config", encoding="utf-8", xml_declaration=True)
    subprocess.run(
        [
            "dotnet",
            "restore",
            str(project / "Consumer.csproj"),
            "--configfile",
            str(project / "NuGet.Config"),
        ],
        check=True,
    )
    subprocess.run(["dotnet", "build", str(project / "Consumer.csproj"), "--no-restore"], check=True)
