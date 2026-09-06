#!/usr/bin/env python3
"""
Release and Version Verification Engine (Issue #59)
Model Context Gateway (MCG)

This script validates release integrity across four dimensions:
1. Version synchronization (csproj, useUserStore.ts, CHANGELOG.md, README.md)
2. Markdown relative links and GFM anchor integrity (docs/ and root markdown files)
3. Backend test suite & Release build execution (dotnet test)
4. Frontend code quality, linting, production build, and Vitest suite (npm)

Usage:
    python3 scripts/verify_release.py [options]
    ./scripts/verify-release.sh [options]

Options:
    --skip-tests            Skip backend/frontend test and build suites (runs fast version + links check)
    --skip-links            Skip markdown relative link and anchor validation
    --skip-versions         Skip version synchronization checks
    --check-versions-only   Run only version synchronization checks
    --check-links-only      Run only markdown link and anchor checks
    --check-tests-only      Run only test and build checks
    --ci                    CI execution mode (streamlined log output)
    -v, --verbose           Show detailed verbose output for all passing checks
    -h, --help              Show this help message
"""

import argparse
import os
import re
import subprocess
import sys
import urllib.parse
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Set, Tuple

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


# Terminal Colors & Styling
class Colors:
    RESET = "\033[0m"
    BOLD = "\033[1m"
    DIM = "\033[2m"
    RED = "\033[91m"
    GREEN = "\033[92m"
    YELLOW = "\033[93m"
    BLUE = "\033[94m"
    MAGENTA = "\033[95m"
    CYAN = "\033[96m"
    WHITE = "\033[97m"
    BG_RED = "\033[41m"
    BG_GREEN = "\033[42m"


def supports_color() -> bool:
    if os.getenv("NO_COLOR"):
        return False
    if os.getenv("FORCE_COLOR") or os.getenv("CI"):
        return True
    return sys.stdout.isatty()


USE_COLOR = supports_color()


def color(text: str, *styles: str) -> str:
    if not USE_COLOR:
        return text
    return "".join(styles) + text + Colors.RESET


@dataclass
class CheckResult:
    category: str
    name: str
    passed: bool
    details: str = ""
    error: Optional[str] = None
    warning: bool = False


@dataclass
class VerificationSummary:
    results: List[CheckResult] = field(default_factory=list)

    def add(self, result: CheckResult):
        self.results.append(result)

    @property
    def passed(self) -> bool:
        return all(r.passed for r in self.results if not r.warning)

    @property
    def total_passed(self) -> int:
        return sum(1 for r in self.results if r.passed and not r.warning)

    @property
    def total_failed(self) -> int:
        return sum(1 for r in self.results if not r.passed and not r.warning)

    @property
    def total_warnings(self) -> int:
        return sum(1 for r in self.results if r.warning)


class ReleaseVerifier:
    def __init__(self, repo_root: Path, verbose: bool = False, ci_mode: bool = False):
        self.repo_root = repo_root.resolve()
        self.verbose = verbose
        self.ci_mode = ci_mode
        self.summary = VerificationSummary()

    def print_banner(self):
        banner = f"""
{color("==================================================================", Colors.CYAN, Colors.BOLD)}
{color("  🛡️  Model Context Gateway (MCG) - Release & Quality Verification Engine  🛡️  ", Colors.WHITE, Colors.BOLD)}
{color("==================================================================", Colors.CYAN, Colors.BOLD)}
  Repository Root: {color(str(self.repo_root), Colors.DIM)}
"""
        print(banner)

    def print_section(self, title: str, icon: str = "📌"):
        print(f"\n{color(f'{icon}  {title}', Colors.CYAN, Colors.BOLD)}")
        print(color("-" * 66, Colors.DIM))

    def record_check(self, category: str, name: str, passed: bool, details: str = "", error: Optional[str] = None, warning: bool = False):
        result = CheckResult(category=category, name=name, passed=passed, details=details, error=error, warning=warning)
        self.summary.add(result)

        if passed:
            tag = color("[PASS]", Colors.GREEN, Colors.BOLD)
            print(f"  {tag} {name}")
            if details and self.verbose:
                print(f"         {color(details, Colors.DIM)}")
        elif warning:
            tag = color("[WARN]", Colors.YELLOW, Colors.BOLD)
            print(f"  {tag} {name}: {details}")
        else:
            tag = color("[FAIL]", Colors.RED, Colors.BOLD)
            print(f"  {tag} {name}")
            if error:
                print(f"         {color('Error: ' + error, Colors.RED)}")
            if details:
                print(f"         {color(details, Colors.DIM)}")

    # -------------------------------------------------------------------------
    # 1. Version Synchronization Checks
    # -------------------------------------------------------------------------
    def verify_versions(self, expected_version: Optional[str] = None) -> Optional[str]:
        self.print_section("1. Version Synchronization & Consistency", "🏷️")

        csproj_path = self.repo_root / "ModelContextGateway.csproj"
        
        if not csproj_path.exists():
            self.record_check("Version", "Project File Exists", False, error=f"File not found: ModelContextGateway.csproj")
            return None

        csproj_content = csproj_path.read_text(encoding="utf-8")

        # 1a. Extract <Version>
        v_match = re.search(r"<Version>(.*?)</Version>", csproj_content)
        if not v_match:
            self.record_check("Version", "Csproj <Version> Tag", False, error=f"<Version> tag missing in {csproj_path.name}")
            return None
        canonical_version = v_match.group(1).strip()

        # Semver validation
        semver_match = re.match(r"^(\d+)\.(\d+)\.(\d+)(?:-[\w.-]+)?$", canonical_version)
        if not semver_match:
            self.record_check("Version", "Csproj SemVer Syntax", False, error=f"Version '{canonical_version}' does not match Semantic Versioning (X.Y.Z)")
            return None
        self.record_check("Version", f"Canonical Version ({canonical_version}) in {csproj_path.name}", True, f"Found <Version>{canonical_version}</Version>")

        if expected_version:
            if canonical_version == expected_version:
                self.record_check("Version", f"Target Release Version ({expected_version}) Alignment", True, f"Target version matches canonical {canonical_version}")
            else:
                self.record_check("Version", f"Target Release Version ({expected_version}) Alignment", False, error=f"Expected version '{expected_version}', found '{canonical_version}'")

        # 1b. Validate AssemblyVersion and FileVersion
        asm_match = re.search(r"<AssemblyVersion>(.*?)</AssemblyVersion>", csproj_content)
        file_match = re.search(r"<FileVersion>(.*?)</FileVersion>", csproj_content)

        expected_4part = f"{canonical_version}.0"
        if asm_match and (asm_match.group(1).strip() == expected_4part or asm_match.group(1).strip() == canonical_version):
            self.record_check("Version", "Csproj <AssemblyVersion> Alignment", True, f"Matches {asm_match.group(1).strip()}")
        else:
            found = asm_match.group(1).strip() if asm_match else "missing"
            self.record_check("Version", "Csproj <AssemblyVersion> Alignment", False, error=f"Expected '{expected_4part}', found '{found}'")

        if file_match and (file_match.group(1).strip() == expected_4part or file_match.group(1).strip() == canonical_version):
            self.record_check("Version", "Csproj <FileVersion> Alignment", True, f"Matches {file_match.group(1).strip()}")
        else:
            found = file_match.group(1).strip() if file_match else "missing"
            self.record_check("Version", "Csproj <FileVersion> Alignment", False, error=f"Expected '{expected_4part}', found '{found}'")

        # 1c. Validate frontend useUserStore.ts fallback default
        store_paths = [
            self.repo_root / "frontend" / "src" / "shared" / "stores" / "useUserStore.ts",
            self.repo_root / "frontend" / "src" / "stores" / "useUserStore.ts"
        ]
        store_checked = False
        for sp in store_paths:
            if sp.exists():
                store_content = sp.read_text(encoding="utf-8")
                # match version: '4.12.2', // fallback default
                store_v_match = re.search(r"version:\s*['\"]([^'\"]+)['\"]", store_content)
                if store_v_match:
                    store_ver = store_v_match.group(1).strip()
                    rel_sp = sp.relative_to(self.repo_root)
                    if store_ver == canonical_version:
                        self.record_check("Version", f"React Store Fallback Version ({rel_sp})", True, f"Matches version: '{store_ver}'")
                        store_checked = True
                    else:
                        self.record_check("Version", f"React Store Fallback Version ({rel_sp})", False, error=f"Expected '{canonical_version}', found '{store_ver}'")
                        store_checked = True

        if not store_checked:
            self.record_check("Version", "React Store Fallback Version", False, error="No useUserStore.ts defining version string found")

        # 1d. Validate CHANGELOG.md top release row
        changelog_path = self.repo_root / "CHANGELOG.md"
        if changelog_path.exists():
            cl_content = changelog_path.read_text(encoding="utf-8")
            # Parse top table row: | **`v4.12.2`** | 2026-08-14 | ...
            cl_row_match = re.search(r"\|\s*\*\*`v?([0-9.]+)`\*\*\s*\|\s*(\d{4}-\d{2}-\d{2})\s*\|", cl_content)
            if cl_row_match:
                cl_ver = cl_row_match.group(1).strip()
                cl_date = cl_row_match.group(2).strip()
                if cl_ver == canonical_version:
                    self.record_check("Version", "CHANGELOG.md Top Entry Alignment", True, f"Top release row is v{cl_ver} ({cl_date})")
                else:
                    self.record_check("Version", "CHANGELOG.md Top Entry Alignment", False, error=f"Top release row is v{cl_ver}, expected v{canonical_version}")
            else:
                self.record_check("Version", "CHANGELOG.md Top Entry Alignment", False, error="Could not parse top release table row in CHANGELOG.md")
        else:
            self.record_check("Version", "CHANGELOG.md Exists", False, error="CHANGELOG.md not found")

        # 1e. Validate README.md version badge and preview table
        readme_path = self.repo_root / "README.md"
        if readme_path.exists():
            readme_content = readme_path.read_text(encoding="utf-8")

            # Version badge: ![Version](https://img.shields.io/badge/version-v4.12.2-orange?style=for-the-badge)
            badge_match = re.search(r"img\.shields\.io/badge/version-v?([0-9.]+)-", readme_content)
            if badge_match:
                badge_ver = badge_match.group(1).strip()
                if badge_ver == canonical_version:
                    self.record_check("Version", "README.md Version Badge Alignment", True, f"Shield badge displays v{badge_ver}")
                else:
                    self.record_check("Version", "README.md Version Badge Alignment", False, error=f"Badge has v{badge_ver}, expected v{canonical_version}")
            else:
                self.record_check("Version", "README.md Version Badge Alignment", False, error="Could not find version shield badge in README.md")

            # README top release table row
            readme_row_match = re.search(r"\|\s*\*\*`v?([0-9.]+)`\*\*\s*\|\s*(\d{4}-\d{2}-\d{2})\s*\|", readme_content)
            if readme_row_match:
                readme_ver = readme_row_match.group(1).strip()
                readme_date = readme_row_match.group(2).strip()
                if readme_ver == canonical_version:
                    self.record_check("Version", "README.md Release Preview Top Entry", True, f"Preview table top release is v{readme_ver} ({readme_date})")
                else:
                    self.record_check("Version", "README.md Release Preview Top Entry", False, error=f"Preview table top release is v{readme_ver}, expected v{canonical_version}")
            else:
                self.record_check("Version", "README.md Release Preview Top Entry", False, error="Could not parse top release row in README.md preview table")
        else:
            self.record_check("Version", "README.md Exists", False, error="README.md not found")

        return canonical_version

    # -------------------------------------------------------------------------
    # 2. Markdown Relative Link & Anchor Verification
    # -------------------------------------------------------------------------
    def _gfm_anchor(self, heading: str) -> str:
        """Generates GitHub Flavored Markdown (GFM) anchor slug."""
        h = heading.strip().lower()
        # Remove markdown links inside headings [text](url) -> text
        h = re.sub(r"\[([^\]]+)\]\([^\)]+\)", r"\1", h)
        # Strip code formatting ticks
        h = h.replace("`", "")
        # Remove emojis and punctuation, leaving letters, numbers, spaces, underscores, hyphens
        h = re.sub(r"[^\w\s-]", "", h).strip()
        # Convert spaces to hyphens
        h = h.replace(" ", "-")
        return h

    def _extract_anchors_from_file(self, filepath: Path) -> Set[str]:
        """Extracts all GFM heading anchors and explicit HTML anchors in a markdown file."""
        anchors: Set[str] = set()
        if not filepath.exists() or not filepath.is_file():
            return anchors

        try:
            content = filepath.read_text(encoding="utf-8", errors="ignore")
        except Exception:
            return anchors

        # Track heading occurrences for duplicate slug suffixing (e.g. -1, -2)
        heading_counts: Dict[str, int] = {}

        for line in content.splitlines():
            # Match Markdown Headings: # Heading, ## Heading, etc.
            h_match = re.match(r"^#{1,6}\s+(.+)$", line)
            if h_match:
                raw_heading = h_match.group(1).strip()
                slug = self._gfm_anchor(raw_heading)
                if slug:
                    count = heading_counts.get(slug, 0)
                    heading_counts[slug] = count + 1
                    if count == 0:
                        anchors.add(slug)
                        # Also add normalized single-hyphen fallback
                        anchors.add(re.sub(r"-+", "-", slug))
                    else:
                        suffixed = f"{slug}-{count}"
                        anchors.add(suffixed)
                        anchors.add(re.sub(r"-+", "-", suffixed))

            # Match explicit HTML anchors: <a id="foo"> or <a name="bar">
            for a_match in re.finditer(r"<a\s+(?:id|name)=[\'\"]([^\'\"]+)[\'\"]", line, re.IGNORECASE):
                anchor_name = a_match.group(1).lower()
                anchors.add(anchor_name)
                anchors.add(re.sub(r"-+", "-", anchor_name))

        return anchors

    def verify_markdown_links(self):
        self.print_section("2. Markdown Link & Anchor Integrity", "🔗")

        # Discover all markdown files
        ignored_dirs = {".git", "node_modules", "bin", "obj", "TestResults", "test-results", "playwright-report", "coverage"}
        md_files: List[Path] = []

        for root, dirs, files in os.walk(self.repo_root):
            # Prune ignored directories in-place
            dirs[:] = [d for d in dirs if d not in ignored_dirs and not d.startswith(".worktrees")]
            for f in files:
                if f.endswith(".md"):
                    md_files.append(Path(root) / f)

        if not md_files:
            self.record_check("Markdown", "Discover Documentation Files", False, error="No .md files found in repository")
            return

        self.record_check("Markdown", f"Scanned Markdown Files ({len(md_files)} files discovered)", True)

        broken_links: List[Tuple[Path, int, str, str]] = []
        total_links_checked = 0

        # Anchor cache per file to avoid redundant file I/O
        anchor_cache: Dict[Path, Set[str]] = {}

        for md_file in md_files:
            rel_file = md_file.relative_to(self.repo_root)
            try:
                content = md_file.read_text(encoding="utf-8", errors="ignore")
            except Exception as e:
                broken_links.append((rel_file, 1, str(rel_file), f"Failed to read file: {e}"))
                continue

            # Strip fenced code blocks to prevent false positive link parsing
            no_code = re.sub(r"```[\s\S]*?```", "", content)

            # Find markdown links [text](url) and reference links [ref]: url
            # Track line numbers by splitting into lines
            for line_idx, line in enumerate(no_code.splitlines(), start=1):
                # Ignore inline code blocks within the line
                clean_line = re.sub(r"`[^`]*`", "", line)

                # Match [text](target)
                inline_links = re.findall(r"\[(?:[^\]]*)\]\(([^)]+)\)", clean_line)
                # Match [ref]: target
                ref_links = re.findall(r"^\[(?:[^\]]+)\]:\s*(\S+)", clean_line)

                all_targets = [t.strip() for t in inline_links + ref_links]

                for target in all_targets:
                    # Clean title attributes if present, e.g. [text](url "title")
                    target = target.split()[0].strip()
                    if not target:
                        continue

                    # Filter out external links & non-relative protocols
                    if re.match(r"^(https?|mailto|tel|ftp|javascript|file|conversation):", target, re.IGNORECASE):
                        continue

                    total_links_checked += 1

                    # Parse path and optional anchor
                    parts = target.split("#", 1)
                    path_str = parts[0].strip()
                    anchor_str = parts[1].strip() if len(parts) > 1 else None

                    # Resolve target file path
                    if not path_str:
                        target_file = md_file
                    else:
                        decoded_path = urllib.parse.unquote(path_str)
                        if decoded_path.startswith("/"):
                            target_file = (self.repo_root / decoded_path.lstrip("/")).resolve()
                        else:
                            target_file = (md_file.parent / decoded_path).resolve()

                    # 1. Verify target file exists
                    if not target_file.exists():
                        broken_links.append((rel_file, line_idx, target, f"Target file does not exist: {path_str}"))
                        continue

                    # 2. If target is markdown and has an anchor, verify anchor existence
                    if anchor_str and target_file.suffix.lower() == ".md":
                        if target_file not in anchor_cache:
                            anchor_cache[target_file] = self._extract_anchors_from_file(target_file)

                        anchors = anchor_cache[target_file]
                        clean_anchor = urllib.parse.unquote(anchor_str).lower()
                        norm_anchor = re.sub(r"-+", "-", clean_anchor)

                        if clean_anchor not in anchors and norm_anchor not in anchors:
                            target_rel = target_file.relative_to(self.repo_root) if target_file.is_relative_to(self.repo_root) else target_file
                            broken_links.append((rel_file, line_idx, target, f"Anchor #{anchor_str} not found in {target_rel}"))

        if not broken_links:
            self.record_check("Markdown", f"Relative Links & Anchor Validity ({total_links_checked} links verified)", True)
        else:
            self.record_check("Markdown", f"Relative Links & Anchor Validity ({len(broken_links)} broken links found)", False)
            for src_file, line_no, target, reason in broken_links:
                print(f"       {color(f'• {src_file}:{line_no}', Colors.YELLOW)} -> {color(target, Colors.BOLD)} ({color(reason, Colors.RED)})")

    # -------------------------------------------------------------------------
    # 3. Test & Build Execution Checks
    # -------------------------------------------------------------------------
    def _run_command(self, cmd: List[str], cwd: Path, name: str, env: Optional[Dict[str, str]] = None) -> bool:
        cmd_str = " ".join(cmd)
        if self.verbose or self.ci_mode:
            print(f"  {color('Executing:', Colors.DIM)} {cmd_str} (in {cwd.relative_to(self.repo_root)})")

        exec_env = os.environ.copy()
        if env:
            exec_env.update(env)

        try:
            result = subprocess.run(
                cmd,
                cwd=cwd,
                env=exec_env,
                capture_output=True,
                text=True,
                check=False,
                shell=(sys.platform == "win32")
            )
            if result.returncode == 0:
                self.record_check("Build & Tests", name, True, f"Command exited cleanly: {cmd_str}")
                return True
            else:
                stderr_output = result.stderr.strip() or result.stdout.strip()
                last_lines = "\n".join(stderr_output.splitlines()[-10:])
                self.record_check("Build & Tests", name, False, error=f"Exit code {result.returncode}\n{last_lines}")
                return False
        except Exception as e:
            self.record_check("Build & Tests", name, False, error=f"Failed to execute command '{cmd_str}': {e}")
            return False

    def verify_backend_tests(self):
        self.print_section("3. Backend .NET Build & Test Verification", "🧪")
        sln_path = self.repo_root / "ModelContextGateway.slnx"
        if not sln_path.exists():
            sln_path = self.repo_root / "McpRouter.slnx"
        if not sln_path.exists():
            self.record_check("Build & Tests", "Gateway Solution Exists", False, error="ModelContextGateway.slnx not found")
            return

        # Backend test command
        cmd = ["dotnet", "test", str(sln_path), "--configuration", "Release"]
        self._run_command(cmd, self.repo_root, ".NET Backend Test Suite (500+ tests)", env={"CI": "true"})

    def verify_frontend_quality(self):
        self.print_section("4. Frontend Quality, Lint, Build & Vitest Verification", "⚛️")
        frontend_dir = self.repo_root / "frontend"
        if not frontend_dir.exists():
            self.record_check("Build & Tests", "Frontend Directory Exists", False, error="frontend/ not found")
            return

        # Ensure node_modules exists or npm ci has been run
        node_modules = frontend_dir / "node_modules"
        if not node_modules.exists():
            print(f"  {color('Installing frontend dependencies via npm ci...', Colors.DIM)}")
            self._run_command(["npm", "ci"], frontend_dir, "Frontend npm ci")

        # 4a. ESLint
        self._run_command(["npm", "run", "lint"], frontend_dir, "Frontend ESLint Quality Check (0 warnings)")

        # 4b. Vite Build
        self._run_command(["npm", "run", "build"], frontend_dir, "Frontend Vite Production Build (SPA)")

        # 4c. Vitest Unit Suite
        self._run_command(["npm", "test"], frontend_dir, "Frontend Vitest Component & Store Suite")

    # -------------------------------------------------------------------------
    # Execution Runner & Report Summary
    # -------------------------------------------------------------------------
    def run(self, check_versions: bool = True, check_links: bool = True, check_tests: bool = True, expected_version: Optional[str] = None) -> int:
        self.print_banner()

        if check_versions:
            self.verify_versions(expected_version=expected_version)

        if check_links:
            self.verify_markdown_links()

        if check_tests:
            self.verify_backend_tests()
            self.verify_frontend_quality()

        # Print Final Summary Table
        print(f"\n{color('==================================================================', Colors.CYAN, Colors.BOLD)}")
        print(color("  📊  Release Verification Summary Report  📊  ", Colors.WHITE, Colors.BOLD))
        print(color("==================================================================", Colors.CYAN, Colors.BOLD))
        print(f"  Total Checks:    {color(str(len(self.summary.results)), Colors.BOLD)}")
        print(f"  Passed Checks:   {color(str(self.summary.total_passed), Colors.GREEN, Colors.BOLD)}")
        print(f"  Failed Checks:   {color(str(self.summary.total_failed), Colors.RED if self.summary.total_failed > 0 else Colors.GREEN, Colors.BOLD)}")
        if self.summary.total_warnings > 0:
            print(f"  Warnings:        {color(str(self.summary.total_warnings), Colors.YELLOW, Colors.BOLD)}")

        print(color("-" * 66, Colors.DIM))
        if self.summary.passed:
            print(f"  {color('🎉 ALL RELEASE & QUALITY GATES PASSED CLEANLY! 🎉', Colors.GREEN, Colors.BOLD)}")
            print(color("==================================================================\n", Colors.CYAN, Colors.BOLD))
            return 0
        else:
            print(f"  {color('❌ ONE OR MORE VERIFICATION GATES FAILED. PLEASE FIX ABOVE ERRORS. ❌', Colors.RED, Colors.BOLD)}")
            print(color("==================================================================\n", Colors.CYAN, Colors.BOLD))
            return 1


def main():
    parser = argparse.ArgumentParser(description="Model Context Gateway (MCG) Release & Quality Verification Engine")
    parser.add_argument("version", nargs="?", default=None, help="Target release version to verify (optional, e.g. 5.10.0)")
    parser.add_argument("--skip-tests", action="store_true", help="Skip backend/frontend test and build execution")
    parser.add_argument("--skip-links", action="store_true", help="Skip markdown link and anchor verification")
    parser.add_argument("--skip-versions", action="store_true", help="Skip version synchronization checks")
    parser.add_argument("--check-versions-only", action="store_true", help="Run only version synchronization checks")
    parser.add_argument("--check-links-only", action="store_true", help="Run only markdown link and anchor checks")
    parser.add_argument("--check-tests-only", action="store_true", help="Run only test and build checks")
    parser.add_argument("--ci", action="store_true", help="CI execution mode")
    parser.add_argument("-v", "--verbose", action="store_true", help="Verbose output")

    args = parser.parse_args()

    repo_root = Path(__file__).resolve().parent.parent

    # Determine execution filters
    check_versions = True
    check_links = True
    check_tests = True

    if args.check_versions_only:
        check_versions, check_links, check_tests = True, False, False
    elif args.check_links_only:
        check_versions, check_links, check_tests = False, True, False
    elif args.check_tests_only:
        check_versions, check_links, check_tests = False, False, True
    else:
        if args.skip_versions:
            check_versions = False
        if args.skip_links:
            check_links = False
        if args.skip_tests:
            check_tests = False

    verifier = ReleaseVerifier(repo_root=repo_root, verbose=args.verbose, ci_mode=args.ci)
    exit_code = verifier.run(
        check_versions=check_versions,
        check_links=check_links,
        check_tests=check_tests,
        expected_version=args.version
    )
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
