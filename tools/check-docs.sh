#!/usr/bin/env bash
# 文件檢查：C# 範例可編譯且測試通過、中英版程式碼一致、相對連結與錨點有效、每行不超過 120 字元
# 用法：bash tools/check-docs.sh（CI 與本機相同；需要 dotnet 與 perl）
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"

# 中英成對的文件，列出英文版路徑（不含 .md）；中文版是同名的 .zh-TW.md
pairs=(
    README
    docs/usage
    docs/writing-providers
    docs/agents/using-systemone
    docs/agents/extending-systemone
)

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Windows 的 Git Bash 下要把路徑轉成 MSBuild 看得懂的形式
native() {
    cygpath -m "$1" 2>/dev/null || echo "$1"
}

failed=0

# 1. 每行不超過 120 字元；Markdown 表格列不受限（規則 018）
echo "== line length"
if ! git ls-files '*.md' | xargs perl -CSD -ne '
    s/\r?\n\z//;
    if (!/^\|/ && length > 120) { printf "%s:%d: %d characters\n", $ARGV, $., length; $bad = 1 }
    close ARGV if eof;
    END { exit($bad ? 1 : 0) }'; then
    failed=1
fi

# 2. 相對連結指向存在的檔案，#錨點對得上目標檔案的標題（GitHub 的錨點規則）
echo "== links"
if ! git ls-files '*.md' | xargs perl -CSD -e '
    use File::Basename;
    use File::Spec;
    sub anchors {
        my ($file) = @_;
        my (%found, $code);
        open my $in, "<:encoding(UTF-8)", $file or return \%found;
        while (<$in>) {
            # 只看程式碼區塊以外的標題行
            s/\r?\n\z//;
            $code = !$code if /^```/;
            next if $code || !/^#+\s+(.*)$/;

            # 照 GitHub 的規則把標題轉成錨點：小寫、去掉標點、空白換成連字號
            my $slug = lc $1;
            $slug =~ s/[^\p{L}\p{N}\s_-]//g;
            $slug =~ s/ /-/g;
            $found{$slug} = 1;
        }
        return \%found;
    }
    my $bad = 0;
    for my $file (@ARGV) {
        # 逐檔掃描程式碼區塊以外的每個 Markdown 連結
        open my $in, "<:encoding(UTF-8)", $file or die "$file: $!";
        my $code;
        while (<$in>) {
            my $line = $.;
            $code = !$code if /^```/;
            next if $code;
            while (/\]\(([^)\s]+)\)/g) {
                # 外部連結不檢查；只寫錨點時，目標就是檔案自己
                my $link = $1;
                next if $link =~ /^(https?:|mailto:)/;
                my ($path, $anchor) = split /#/, $link, 2;
                my $target = $path eq "" ? $file : File::Spec->catfile(dirname($file), $path);
                if (!-e $target) {
                    # 目標檔案不存在
                    print "$file:$line: missing file $link\n";
                    $bad = 1;
                }
                elsif (defined $anchor && $target =~ /\.md$/ && !anchors($target)->{$anchor}) {
                    # 檔案存在，但錨點對不上任何標題
                    print "$file:$line: missing anchor $link\n";
                    $bad = 1;
                }
            }
        }
    }
    exit $bad;'; then
    failed=1
fi

# 3. 中英版的程式碼區塊相同（註解行可以不同語言）
echo "== English and Chinese code blocks match"
code_blocks() {
    perl -CSD -ne '
        s/\r?\n\z//;
        if (/^```/) { $code = !$code; print "```\n"; next }
        print "$_\n" if $code && !m{^\s*//}' "$1"
}
for doc in "${pairs[@]}"; do
    if ! diff <(code_blocks "$doc.md") <(code_blocks "$doc.zh-TW.md") > "$work/diff.txt"; then
        echo "$doc.md and $doc.zh-TW.md have different code:"
        head -20 "$work/diff.txt"
        failed=1
    fi
done

# 4. 抽出每個 ```csharp 區塊：型別宣告原樣成檔，其餘陳述式包進方法；中英各編譯一次（型別名稱相同）
extract() {
    local out="$1"
    shift
    mkdir -p "$out"
    perl -CSD -e '
        my $out = shift;
        # 型別宣告開頭可能出現的修飾詞
        my $modifiers = qr/(public |internal )?((sealed|static|abstract|partial) )*/;
        for my $md (@ARGV) {
            # 檔名轉成識別項片段，用在產生的檔名與方法名
            (my $base = $md) =~ s{.*/}{};
            $base =~ s/\.md$//;
            $base =~ tr/.-/__/;
            open my $in, "<:encoding(UTF-8)", $md or die "$md: $!";
            my ($inside, $count, @lines) = (0, 0);
            while (<$in>) {
                s/\r?\n\z//;
                if (!$inside && /^```csharp\s*$/) { ($inside, @lines) = (1); $count++; next }
                if ($inside && /^```\s*$/) {
                    # 區塊結束：整段寫成一個 .cs 檔
                    $inside = 0;
                    my $text = join "\n", @lines;
                    # 介面簽章只是展示，宣告了會與函式庫的同名型別衝突
                    next if $text =~ /public interface ISystemOne/;
                    open my $cs, ">:encoding(UTF-8)", "$out/${base}_$count.cs" or die $!;
                    if ($text =~ /^${modifiers}(class|record|interface) /m) {
                        # 型別宣告原樣寫出
                        print $cs "$text\n";
                    }
                    else {
                        # 陳述式包進 Snippets 的一個方法
                        print $cs "internal static partial class Snippets\n{\n";
                        print $cs "    private static async Task ${base}_$count()\n    {\n";
                        print $cs map { "        $_\n" } @lines;
                        print $cs "    }\n}\n";
                    }
                    close $cs;
                    next;
                }
                push @lines, $_ if $inside;
            }
        }' "$out" "$@"
}

english=()
chinese=()
for doc in "${pairs[@]}"; do
    english+=("$doc.md")
    chinese+=("$doc.zh-TW.md")
done
extract "$work/en" "${english[@]}"
extract "$work/zh" "${chinese[@]}"

# 範例之間共用、或接續上一段的變數
cat > "$work/Ambient.cs" <<'EOF'
internal static partial class Snippets
{
    private static string apiKey = "";
    private static ISystemOne systemOne = SystemOne.Providers.UseMock();
    private static SystemOne.Request request = SystemOne.Requests.Noul("a", "b").For("x");
    private static SystemOne.Response response = SystemOne.Factory.Response("m");
    private static CancellationToken cancellationToken;
    private static object ticket = new();
    private static byte[] bytes = [];
    private static JsonObject signals = new();
    private static (string Name, string Question)[] flags = [];
    private static IServiceCollection services = new ServiceCollection();
    private static MockSystemOneProvider happy = SystemOne.Providers.UseMock();
}
EOF

# 編譯範例用的測試專案：參考 src 下每個套件，SnippetDir 指定要編譯哪一組範例
csproj="$(cat <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS1998;CS0414;CS0169;CS8321;CS0219;CS0168;CS0649</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Ambient.cs" />
    <Compile Include="$(SnippetDir)/*.cs" />
    <ProjectReference Include="@ROOT@/src/*/*.csproj" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="9.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <Using Include="System.Globalization" />
    <Using Include="System.Net" />
    <Using Include="System.Text.Json" />
    <Using Include="System.Text.Json.Nodes" />
    <Using Include="Microsoft.Extensions.DependencyInjection" />
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
EOF
)"
# 專案寫到存放庫外的暫存目錄，@ROOT@ 換成存放庫的絕對路徑
printf '%s\n' "${csproj//@ROOT@/$(native "$root")}" > "$work/DocsCheck.csproj"

# 中英兩組範例各編譯、執行測試一次
for language in en zh; do
    echo "== C# snippets ($language: $(ls "$work/$language" | wc -l | tr -d ' ') blocks)"
    if ! dotnet test \
        "$(native "$work/DocsCheck.csproj")" \
        -p:SnippetDir="$(native "$work/$language")" \
        -nologo \
        -v q
    then
        failed=1
    fi
done

if [ "$failed" -ne 0 ]; then
    echo "Documentation check failed."
    exit 1
fi

echo "Documentation check passed."