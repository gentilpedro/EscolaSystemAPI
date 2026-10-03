---
name: release-standards
description: >
  Padroniza versionamento (Semantic Versioning), geração de artefatos e
  publicação de GitHub Releases dos projetos. Use esta skill sempre que for
  criar uma release, publicar uma nova versão, decidir se o próximo bump é
  MAJOR/MINOR/PATCH, criar ou empurrar uma tag vX.Y.Z, gerar changelog ou
  release notes, ou quando o usuário disser algo como "lança uma nova
  versão", "prepara o release", "sobe pra produção", "gera o release
  v1.2.0", ou pedir para atualizar o CHANGELOG — mesmo que ele não use as
  palavras "release" ou "versionamento" explicitamente. Consulte também
  antes de gerar qualquer artefato de build destinado a distribuição
  (publish do .NET/Blazor, build de produção do React, executável
  PyInstaller do Python).
---

# Padrões de Release

Uma release representa uma versão do software que foi realmente validada — build passou, testes passaram, o artefato existe e está correto. Nada disso é negociável: se qualquer verificação falhar, a release é abortada, não "contornada" para sair mesmo assim.

Esta skill cobre a decisão de versão e a publicação da release. Para as operações de Git em si (identidade, commits, push, comandos destrutivos) use `git-standards`. Se o projeto já tiver o pipeline da skill `github-cicd` configurado, o job de release ali definido é quem efetivamente builda, testa e publica a release quando a tag é empurrada — esta skill garante que a tag certa seja criada, com o conteúdo certo, e nunca antes das validações passarem.

## Versionamento (Semantic Versioning)

Formato: `MAJOR.MINOR.PATCH` (ex.: `1.0.0`, `1.1.0`, `1.1.1`). Tags no Git sempre com o prefixo `v`: `v1.0.0`, `v1.1.0`, `v1.1.1`.

Regra de incremento:

| Tipo de mudança | Incrementa | Exemplo |
|---|---|---|
| Feature nova | MINOR (zera PATCH) | `1.2.0 → 1.3.0` |
| Correção de bug | PATCH | `1.2.0 → 1.2.1` |
| Breaking change | MAJOR (zera MINOR e PATCH) | `1.2.0 → 2.0.0` |

Nunca invente uma versão sem antes analisar a versão atual e o que mudou desde ela — "que tal a 2.0" não é uma decisão de versionamento, é um palpite. A versão sempre nasce de uma análise do histórico real.

### Use o script de análise, não a memória

Em vez de tentar lembrar ou adivinhar o que mudou desde a última tag, rode:

```bash
python3 <caminho-da-skill>/scripts/next_version.py --repo <caminho-do-repositorio>
```

Ele implementa os passos 1 a 4 do fluxo obrigatório de forma determinística: lista as tags existentes (`git tag --sort=-version:refname`), identifica a última, lê os commits desde ela, classifica cada um por Conventional Commits (`feat:` → feature, `fix:` → correção, `feat!:`/corpo com `BREAKING CHANGE:` → breaking, qualquer outro tipo vai para "outras alterações" e não decide o incremento sozinho) e imprime a versão sugerida com o motivo, mais um rascunho em Markdown já separado em Breaking Changes / Features / Correções — pronto para virar a release note ou a entrada do CHANGELOG.

O script depende de mensagens de commit no padrão Conventional Commits (o mesmo padrão que a skill `git-standards` já pede). Se o histórico do projeto não seguir esse padrão, o script não vai achar nada para classificar — nesse caso, leia `git log <última-tag>..HEAD` manualmente e aplique a mesma lógica de classificação à mão antes de decidir a versão.

A sugestão do script é isso: uma sugestão. Sempre a valide contra o real significado das mudanças antes de seguir — um `feat:` que na prática só renomeou uma variável não é uma feature de verdade, e um `fix:` que na prática mudou um contrato de API é uma breaking change disfarçada.

## Fluxo obrigatório, nesta ordem

1. Verifique as tags existentes (`git tag --sort=-version:refname`, ou deixe o script fazer isso).
2. Identifique a última versão.
3. Analise as alterações desde a última release (`next_version.py`, ou `git log` manual).
4. Determine o incremento adequado (MAJOR/MINOR/PATCH), confirmando a sugestão do script contra o significado real das mudanças.
5. Execute o build.
6. Execute os testes.
7. Gere o artefato (ver "Artefatos por tecnologia" abaixo).
8. Crie a tag (`git tag vX.Y.Z`) e, se o repositório tiver um remoto configurado, empurre-a (`git push origin vX.Y.Z`) — em um repositório ainda sem remoto, a tag local é o suficiente por enquanto; não trate a ausência de remoto como falha do processo.
9. Publique a GitHub Release (só se aplica quando há um repositório remoto no GitHub; sem isso, os passos 1-8 já deixam a versão pronta para quando o remoto existir).

Não pule etapas nem inverta a ordem — em particular, nunca crie a tag (passo 8) antes de build e testes (passos 5-6) terem passado localmente. Se o repositório tiver CI/CD (`github-cicd`), empurrar a tag também aciona a validação remota — mas essa validação remota é uma segunda linha de defesa, não substitui a local: um pipeline quebrado que só falha depois de a tag já existir é pior de desfazer do que um problema pego antes.

## Gate de validação final — aborta em qualquer falha

Antes de criar a tag e publicar a release, confira, nesta ordem:

```bash
git status
git tag
```

mais o build, os testes, e a validação do artefato gerado (ele existe, não está vazio, tem o formato esperado). Se **qualquer** uma dessas etapas falhar — `git status` mostrando algo inesperado, a tag já existindo, build quebrado, teste falhando, artefato ausente ou corrompido — **aborte a release**. Informe o que falhou e pare; não tente contornar a falha (pular o teste que está falhando, usar um artefato de uma build anterior, forçar a tag mesmo assim) só para conseguir entregar um número de versão. Uma release que existe mas não representa código validado é pior do que nenhuma release.

## Conteúdo da GitHub Release

Toda GitHub Release deve conter:

- a versão (tag)
- um título
- um resumo das alterações
- as principais features
- as correções
- breaking changes, quando existirem
- os artefatos, quando aplicável

O rascunho gerado por `next_version.py` já cobre a estrutura de Breaking Changes / Features / Correções — use-o como base e escreva o resumo/título por cima.

Se o repositório usa o pipeline de `github-cicd`, o job de release de lá já cria a Release automaticamente ao ver a tag, usando `generate_release_notes: true` (notas geradas pelo próprio GitHub a partir dos PRs). Essas notas automáticas não têm necessariamente a estrutura acima. Depois que a Release existir, revise-a e, se as notas automáticas não cobrirem features/correções/breaking changes com clareza, sobrescreva com `gh release edit <tag> --notes-file <arquivo>` usando o rascunho estruturado. Se o repositório não tiver esse pipeline, crie a Release diretamente com `gh release create <tag> --title "<título>" --notes-file <arquivo> <artefatos...>`.

## CHANGELOG.md

Se o projeto já tem um `CHANGELOG.md`, atualize-o — não crie um novo arquivo nem recrie o changelog existente em outro formato (ex.: não troque um changelog em texto livre por um em "Keep a Changelog" só porque é o padrão mais comum; siga o formato que o projeto já usa). Insira a nova entrada de versão seguindo a mesma estrutura das entradas anteriores (olhe para a entrada mais recente do arquivo antes de escrever a nova).

Se o projeto não tem `CHANGELOG.md`, não é obrigatório criar um só por causa desta skill — as release notes no GitHub já cobrem o registro do que mudou, a menos que o desenvolvedor peça um changelog no repositório.

## Artefatos por tecnologia

Gere sempre o artefato correspondente à tecnologia do projeto (as mesmas categorias de `github-cicd`):

- **.NET**: `dotnet publish` apropriado para o tipo de aplicação (API, worker, etc.).
- **Blazor**: `dotnet publish` apropriado (Server ou WASM, conforme o projeto).
- **React**: build de produção (`npm run build`).
- **Python**: se o projeto usa PyInstaller (verifique se `pyinstaller` está nas dependências), gere o executável com ele. Se não usa PyInstaller, gere o artefato de distribuição padrão do projeto (ex.: `python -m build` para um pacote com `[build-system]` configurado); não invente um executável para um projeto que não foi desenhado para virar um.

Se o projeto não se encaixa em nenhum desses casos (sem PyInstaller, sem `[build-system]`, sem um passo de "publish" próprio da tecnologia), gere o artefato como um snapshot limpo do código na versão taggeada — `git archive --format=zip -o dist/<nome-do-projeto>-vX.Y.Z.zip HEAD` é a forma mais segura, porque empacota exatamente o que está commitado, sem arrastar arquivos ignorados ou de build local por engano. Nomeie sempre o artefato com o nome do projeto e a versão (`<projeto>-vX.Y.Z.<ext>`), para que múltiplos artefatos anexados a uma Release nunca fiquem ambíguos.

Nunca publique arquivos temporários no artefato ou na Release: exclua diretórios de build intermediários, caches, `__pycache__`, `node_modules`, `bin/`/`obj/` que não fazem parte do publish final, e qualquer coisa que não seria distribuída para quem usa o software.

## Objetivo final

Uma release só deve existir se representar uma versão do software que foi de fato build, testada e empacotada com sucesso — nunca uma que "provavelmente funciona" ou que foi gerada pulando uma etapa que falhou.
