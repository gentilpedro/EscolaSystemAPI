#!/usr/bin/env python3
"""
next_version.py — implementa os passos 1-4 do fluxo obrigatório de release
(verificar tags existentes, identificar a última versão, analisar as
alterações desde ela, e determinar o incremento adequado de SemVer).

Não decide sozinho e não cria nada: apenas lê o histórico do git e devolve
uma sugestão determinística + os dados que embasam a decisão, para que quem
está conduzindo a release (humano ou Claude) confirme antes de agir.

Uso:
    python3 next_version.py [--repo PATH]

Saída: um relatório em texto com
  - a última tag encontrada (ou aviso de que não há nenhuma)
  - os commits desde essa tag, classificados em breaking / features / fixes / outros
  - a versão sugerida e o motivo do incremento escolhido
  - um rascunho em Markdown pronto para virar release notes / entrada de CHANGELOG

Classificação de commits: segue Conventional Commits.
  - "feat: ..."        -> feature (incrementa MINOR)
  - "fix: ..."         -> correção (incrementa PATCH)
  - "feat!: ..." ou corpo com "BREAKING CHANGE:" / "BREAKING-CHANGE:" -> breaking (incrementa MAJOR)
  - qualquer outro tipo (chore, docs, refactor, test, ci, style, perf, build...) -> listado
    como "outras alterações", mas não decide sozinho o incremento de versão.

Se o histórico não tiver nenhum commit seguindo Conventional Commits, o script
avisa isso explicitamente em vez de arriscar um palpite — quem estiver conduzindo
a release deve então analisar manualmente o que mudou.
"""
import argparse
import re
import subprocess
import sys

TAG_RE = re.compile(r"^v(\d+)\.(\d+)\.(\d+)$")
SUBJECT_RE = re.compile(r"^(?P<type>\w+)(\((?P<scope>[^)]+)\))?(?P<breaking>!)?:\s*(?P<desc>.+)$")


def run(args, cwd):
    result = subprocess.run(args, cwd=cwd, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"Comando falhou: {' '.join(args)}\n{result.stderr}")
    return result.stdout


def latest_tag(repo):
    try:
        out = run(["git", "tag", "--sort=-version:refname"], repo)
    except RuntimeError as e:
        print(f"Não foi possível listar tags: {e}", file=sys.stderr)
        sys.exit(1)
    for line in out.splitlines():
        line = line.strip()
        if TAG_RE.match(line):
            return line
    return None


def commits_since(repo, tag):
    rev_range = f"{tag}..HEAD" if tag else "HEAD"
    fmt = "%H%x01%s%x02%b%x03"
    try:
        out = run(["git", "log", rev_range, f"--pretty=format:{fmt}"], repo)
    except RuntimeError as e:
        print(f"Não foi possível ler o histórico de commits: {e}", file=sys.stderr)
        sys.exit(1)
    commits = []
    for raw in out.split("\x03"):
        raw = raw.strip("\n")
        if not raw.strip():
            continue
        head_rest = raw.split("\x01", 1)
        if len(head_rest) != 2:
            continue
        sha, rest = head_rest
        subject_body = rest.split("\x02", 1)
        if len(subject_body) != 2:
            continue
        subject, body = subject_body
        commits.append({"sha": sha.strip(), "subject": subject.strip(), "body": body.strip()})
    return commits


def classify(commits):
    breaking, features, fixes, others = [], [], [], []
    for c in commits:
        m = SUBJECT_RE.match(c["subject"])
        is_breaking_body = "BREAKING CHANGE:" in c["body"] or "BREAKING-CHANGE:" in c["body"]
        if not m:
            others.append(c)
            continue
        ctype = m.group("type").lower()
        desc = m.group("desc")
        is_breaking = bool(m.group("breaking")) or is_breaking_body
        entry = {**c, "type": ctype, "desc": desc}
        if is_breaking:
            breaking.append(entry)
        elif ctype == "feat":
            features.append(entry)
        elif ctype == "fix":
            fixes.append(entry)
        else:
            others.append(entry)
    return breaking, features, fixes, others


def bump(tag, breaking, features, fixes):
    if tag is None:
        return None, "sem tag anterior — esta seria a primeira release; escolha manualmente a versão inicial (ex.: v0.1.0 para pré-lançamento ou v1.0.0 para a primeira versão estável)"
    major, minor, patch = (int(x) for x in TAG_RE.match(tag).groups())
    if breaking:
        return f"v{major + 1}.0.0", "há BREAKING CHANGE(s) desde a última tag -> incrementa MAJOR"
    if features:
        return f"v{major}.{minor + 1}.0", "há feature(s) nova(s) desde a última tag, sem breaking changes -> incrementa MINOR"
    if fixes:
        return f"v{major}.{minor}.{patch + 1}", "há correção(ões) desde a última tag, sem features nem breaking changes -> incrementa PATCH"
    return None, "nenhum commit feat/fix/breaking encontrado desde a última tag — analise manualmente se há motivo real para uma nova release"


def format_section(title, entries):
    if not entries:
        return ""
    lines = [f"### {title}"]
    for e in entries:
        scope = f"**{e['scope']}**: " if e.get("scope") else ""
        desc = e.get("desc", e["subject"])
        lines.append(f"- {scope}{desc} ({e['sha'][:7]})")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--repo", default=".", help="Caminho do repositório (padrão: diretório atual)")
    args = parser.parse_args()

    tag = latest_tag(args.repo)
    commits = commits_since(args.repo, tag)
    breaking, features, fixes, others = classify(commits)
    suggestion, reason = bump(tag, breaking, features, fixes)

    print("=" * 70)
    print("ANÁLISE DE VERSÃO — release-standards")
    print("=" * 70)
    print(f"Última tag encontrada : {tag or '(nenhuma — repositório sem releases ainda)'}")
    print(f"Commits desde a última: {len(commits)}")
    print(f"Sugestão de versão    : {suggestion or '(nenhuma sugerida automaticamente)'}")
    print(f"Motivo                : {reason}")
    print()

    if not commits:
        print("Nenhum commit novo desde a última tag. Não há o que lançar.")
        return

    print("-" * 70)
    print("Rascunho de release notes / entrada de CHANGELOG (Markdown):")
    print("-" * 70)
    draft = []
    if suggestion:
        draft.append(f"## {suggestion}\n")
    else:
        draft.append(f"## <definir versão>\n")
    draft.append(format_section("⚠ Breaking Changes", breaking))
    draft.append(format_section("Features", features))
    draft.append(format_section("Correções", fixes))
    if others:
        draft.append(format_section("Outras alterações", others))
    print("\n".join(d for d in draft if d))

    if others and not (breaking or features or fixes):
        print(
            "Atenção: todos os commits encontrados são de tipos que não decidem o incremento "
            "sozinhos (chore/docs/refactor/test/ci/style/build/perf...). Confirme manualmente "
            "se algum deles de fato merece virar uma release antes de prosseguir.",
            file=sys.stderr,
        )


if __name__ == "__main__":
    main()
