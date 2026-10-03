"""Issue -> branch -> PR/MR para qualquer repositório no GitHub ou no GitLab.

O host e o projeto saem do `git remote get-url origin` do repositório onde o comando roda, então o
mesmo script serve para qualquer projeto. Textos longos (descrição de issue e PR/MR) entram por
arquivo, para não brigar com aspas e acentos no shell do Windows.

- GitHub: usa o `gh` CLI, que já cuida da autenticação (`gh auth login`, feito pelo usuário).
- GitLab: usa a API REST com o token de ~/.gitlab-token, que nunca é impresso nem lido do chat.

Uso (de dentro do repositório):
  python fluxo.py info
  python fluxo.py criar-issue   --titulo T --descricao-arquivo F --tipo feature|fix|hotfix
  python fluxo.py ver-issue     --numero N
  python fluxo.py criar-branch  --nome N
  python fluxo.py criar-pr      --branch N --titulo T --descricao-arquivo F [--draft]
  python fluxo.py atualizar-pr  --numero N [--titulo T] [--descricao-arquivo F] [--pronto]
  python fluxo.py status        --numero N --para todo|andamento|review
  python fluxo.py preparar-labels

Labels de tipo e de status vêm do config.json ao lado da pasta scripts/. Label de status que não
existe no repositório é ignorada (o `info` lista quais faltam; `preparar-labels` as cria).
"""

import argparse
import functools
import json
import pathlib
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request

PASTA_SKILL = pathlib.Path(__file__).resolve().parent.parent
ARQUIVO_TOKEN_GITLAB = pathlib.Path.home() / ".gitlab-token"


def rodar(*cmd: str) -> str:
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0:
        sys.exit(f"Falhou: {' '.join(cmd[:3])}...\n{r.stderr.strip()}")
    return r.stdout.strip()


def ler(arquivo: str | None) -> str | None:
    return pathlib.Path(arquivo).read_text(encoding="utf-8") if arquivo else None


@functools.cache
def config() -> dict:
    arquivo = PASTA_SKILL / "config.json"
    return json.loads(arquivo.read_text(encoding="utf-8")) if arquivo.exists() else {}


@functools.cache
def remoto() -> tuple[str, str]:
    """(host, projeto) a partir do origin: github.com/dono/repo ou gitlab.com/grupo/sub/repo."""
    url = rodar("git", "remote", "get-url", "origin")
    m = re.match(r"^(?:https?://(?:[^@/]+@)?|git@)([^/:]+)[/:](.+?)(?:\.git)?/?$", url)
    if not m:
        sys.exit(f"Não reconheci o remote origin: {url}")
    host, projeto = m.group(1).lower(), m.group(2)
    if "github" in host:
        return "github", projeto
    if "gitlab" in host:
        return "gitlab", projeto
    sys.exit(f"Host não suportado: {host} (só GitHub e GitLab).")


# ---------------------------------------------------------------- GitHub (gh CLI)

def gh(*args: str) -> str:
    if not shutil.which("gh"):
        sys.exit("O gh CLI não está instalado. Instale e peça ao usuário para rodar `gh auth login`.")
    return rodar("gh", *args, "--repo", remoto()[1])


def numero_da_url(url: str) -> int:
    return int(url.rstrip("/").rsplit("/", 1)[-1])


# ---------------------------------------------------------------- GitLab (API REST)

def token_gitlab() -> str:
    if not ARQUIVO_TOKEN_GITLAB.exists():
        sys.exit(f"Token não encontrado em {ARQUIVO_TOKEN_GITLAB}. Peça ao usuário para salvá-lo lá "
                 "(num terminal próprio, fora do chat).")
    return ARQUIVO_TOKEN_GITLAB.read_text(encoding="utf-8-sig").strip()


def gl(metodo: str, caminho: str, corpo: dict | None = None, *, global_: bool = False) -> dict | list:
    base = "https://gitlab.com/api/v4"
    url = base + caminho if global_ else f"{base}/projects/{urllib.parse.quote(remoto()[1], safe='')}{caminho}"
    dados = json.dumps(corpo).encode("utf-8") if corpo is not None else None
    req = urllib.request.Request(url, data=dados, method=metodo)
    req.add_header("PRIVATE-TOKEN", token_gitlab())
    req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as erro:
        sys.exit(f"GitLab respondeu {erro.code} em {metodo} {caminho}: "
                 f"{erro.read().decode('utf-8', 'replace')}")


@functools.cache
def gl_usuario_id() -> int:
    return gl("GET", "/user", global_=True)["id"]


# ---------------------------------------------------------------- comum aos dois

@functools.cache
def branch_padrao() -> str:
    host, _ = remoto()
    if host == "github":
        # `gh repo view` recebe o repositório como argumento, não por --repo.
        saida = rodar("gh", "repo", "view", remoto()[1], "--json", "defaultBranchRef")
        return json.loads(saida)["defaultBranchRef"]["name"]
    return gl("GET", "")["default_branch"]


@functools.cache
def labels_existentes() -> set[str]:
    host, _ = remoto()
    if host == "github":
        return {l["name"] for l in json.loads(gh("label", "list", "--limit", "200", "--json", "name"))}
    return {l["name"] for l in gl("GET", "/labels?per_page=100")}


def labels_status() -> dict[str, str]:
    return config().get("labels_status", {})


def so_existentes(nomes: list[str]) -> list[str]:
    existentes = labels_existentes()
    return [n for n in nomes if n and n in existentes]


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    p = argparse.ArgumentParser()
    sub = p.add_subparsers(dest="cmd", required=True)

    sub.add_parser("info")

    s = sub.add_parser("criar-issue")
    s.add_argument("--titulo", required=True)
    s.add_argument("--descricao-arquivo", required=True)
    s.add_argument("--tipo", required=True, choices=["feature", "fix", "hotfix"])
    s.add_argument("--labels-extra", default="", help="labels adicionais já existentes, separadas por vírgula")

    s = sub.add_parser("ver-issue")
    s.add_argument("--numero", required=True, type=int)

    s = sub.add_parser("criar-branch")
    s.add_argument("--nome", required=True)

    s = sub.add_parser("criar-pr")
    s.add_argument("--branch", required=True)
    s.add_argument("--titulo", required=True)
    s.add_argument("--descricao-arquivo", required=True)
    s.add_argument("--draft", action="store_true")

    s = sub.add_parser("atualizar-pr")
    s.add_argument("--numero", required=True, type=int)
    s.add_argument("--titulo")
    s.add_argument("--descricao-arquivo")
    s.add_argument("--pronto", action="store_true", help="tira do rascunho/Draft")

    s = sub.add_parser("status")
    s.add_argument("--numero", required=True, type=int, help="número da issue")
    s.add_argument("--para", required=True, choices=["todo", "andamento", "review"])

    sub.add_parser("preparar-labels")

    a = p.parse_args()
    host, projeto = remoto()

    if a.cmd == "info":
        faltando = [n for n in labels_status().values() if n not in labels_existentes()]
        print(json.dumps({"host": host, "projeto": projeto, "branch_padrao": branch_padrao(),
                          "labels_tipo": config().get("labels_tipo", {}),
                          "labels_status": labels_status(),
                          "labels_status_faltando": faltando}, ensure_ascii=False, indent=2))

    elif a.cmd == "criar-issue":
        labels = so_existentes([config().get("labels_tipo", {}).get(a.tipo, ""),
                                labels_status().get("todo", ""),
                                *[l.strip() for l in a.labels_extra.split(",")]])
        if host == "github":
            args = ["issue", "create", "--title", a.titulo, "--body-file", a.descricao_arquivo,
                    "--assignee", "@me"]
            for l in labels:
                args += ["--label", l]
            url = gh(*args)
            print(json.dumps({"numero": numero_da_url(url), "url": url, "labels": labels}, ensure_ascii=False))
        else:
            r = gl("POST", "/issues", {"title": a.titulo, "description": ler(a.descricao_arquivo),
                                       "labels": ",".join(labels), "assignee_ids": [gl_usuario_id()]})
            print(json.dumps({"numero": r["iid"], "url": r["web_url"], "labels": labels}, ensure_ascii=False))

    elif a.cmd == "ver-issue":
        if host == "github":
            r = json.loads(gh("issue", "view", str(a.numero), "--json", "number,title,state,labels,body,url"))
            print(json.dumps({"numero": r["number"], "titulo": r["title"], "estado": r["state"],
                              "labels": [l["name"] for l in r["labels"]], "descricao": r["body"],
                              "url": r["url"]}, ensure_ascii=False, indent=2))
        else:
            r = gl("GET", f"/issues/{a.numero}")
            print(json.dumps({"numero": r["iid"], "titulo": r["title"], "estado": r["state"],
                              "labels": r["labels"], "descricao": r["description"],
                              "url": r["web_url"]}, ensure_ascii=False, indent=2))

    elif a.cmd == "criar-branch":
        # Local + push: funciona igual nos dois hosts e já deixa a branch acompanhando o origin.
        base = branch_padrao()
        rodar("git", "fetch", "origin", base)
        rodar("git", "switch", "-c", a.nome, f"origin/{base}")
        rodar("git", "push", "-u", "origin", a.nome)
        print(json.dumps({"branch": a.nome, "base": base}))

    elif a.cmd == "criar-pr":
        base = branch_padrao()
        if host == "github":
            args = ["pr", "create", "--base", base, "--head", a.branch, "--title", a.titulo,
                    "--body-file", a.descricao_arquivo, "--assignee", "@me"]
            if a.draft:
                args.append("--draft")
            url = gh(*args)
            print(json.dumps({"numero": numero_da_url(url), "url": url}, ensure_ascii=False))
        else:
            r = gl("POST", "/merge_requests", {
                "source_branch": a.branch, "target_branch": base,
                "title": ("Draft: " + a.titulo) if a.draft else a.titulo,
                "description": ler(a.descricao_arquivo),
                "assignee_ids": [gl_usuario_id()], "reviewer_ids": [gl_usuario_id()],
                "remove_source_branch": True, "squash": False,
            })
            print(json.dumps({"numero": r["iid"], "url": r["web_url"]}, ensure_ascii=False))

    elif a.cmd == "atualizar-pr":
        if host == "github":
            edit = ["pr", "edit", str(a.numero)]
            if a.titulo:
                edit += ["--title", a.titulo]
            if a.descricao_arquivo:
                edit += ["--body-file", a.descricao_arquivo]
            if len(edit) > 3:
                gh(*edit)
            if a.pronto and json.loads(gh("pr", "view", str(a.numero), "--json", "isDraft"))["isDraft"]:
                gh("pr", "ready", str(a.numero))
            r = json.loads(gh("pr", "view", str(a.numero), "--json", "number,title,isDraft,url"))
            print(json.dumps({"numero": r["number"], "titulo": r["title"], "rascunho": r["isDraft"],
                              "url": r["url"]}, ensure_ascii=False))
        else:
            corpo: dict = {}
            titulo = a.titulo
            if a.pronto:
                atual = gl("GET", f"/merge_requests/{a.numero}")["title"]
                titulo = (titulo or atual).removeprefix("Draft: ").removeprefix("Draft:").strip()
                corpo["reviewer_ids"] = [gl_usuario_id()]
            if titulo:
                corpo["title"] = titulo
            if a.descricao_arquivo:
                corpo["description"] = ler(a.descricao_arquivo)
            r = gl("PUT", f"/merge_requests/{a.numero}", corpo)
            print(json.dumps({"numero": r["iid"], "titulo": r["title"], "rascunho": r["draft"],
                              "url": r["web_url"]}, ensure_ascii=False))

    elif a.cmd == "status":
        alvo = labels_status().get(a.para, "")
        remover = [n for k, n in labels_status().items() if k != a.para]
        adicionar, remover = so_existentes([alvo]), so_existentes(remover)
        if not adicionar:
            print(json.dumps({"aviso": f"label de status '{alvo}' não existe no repositório; "
                                       "nada mudou (ver `info` / `preparar-labels`)"}, ensure_ascii=False))
            return
        if host == "github":
            args = ["issue", "edit", str(a.numero), "--add-label", ",".join(adicionar)]
            if remover:
                args += ["--remove-label", ",".join(remover)]
            gh(*args)
            r = json.loads(gh("issue", "view", str(a.numero), "--json", "number,labels"))
            print(json.dumps({"numero": r["number"], "labels": [l["name"] for l in r["labels"]]}, ensure_ascii=False))
        else:
            r = gl("PUT", f"/issues/{a.numero}", {"add_labels": ",".join(adicionar),
                                                  "remove_labels": ",".join(remover)})
            print(json.dumps({"numero": r["iid"], "labels": r["labels"]}, ensure_ascii=False))

    elif a.cmd == "preparar-labels":
        cores = {"todo": "ededed", "andamento": "1d76db", "review": "fbca04"}
        criadas = []
        for chave, nome in labels_status().items():
            if nome in labels_existentes():
                continue
            if host == "github":
                gh("label", "create", nome, "--color", cores.get(chave, "ededed"))
            else:
                gl("POST", "/labels", {"name": nome, "color": "#" + cores.get(chave, "ededed")})
            criadas.append(nome)
        print(json.dumps({"criadas": criadas}, ensure_ascii=False))


if __name__ == "__main__":
    main()
