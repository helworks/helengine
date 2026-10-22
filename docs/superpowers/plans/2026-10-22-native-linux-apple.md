# Native Linux and Apple Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar os siblings Linux e Apple, validar os hosts nativos e integrar os targets ao core gerado e ao pipeline de conteúdo, avançando do menu para as cenas de rendering do DemoDisc.

**Architecture:** Linux usa SDL3/Vulkan; macOS e iOS compartilham SDL3/Metal em um sibling Apple. Cada backend consome os contratos existentes de geração e conteúdo; o host comum de build deve funcionar sem WinForms/DirectX11 no Linux e no Mac.

**Tech Stack:** C++20, C#/net9.0 para builders, SDL3, Vulkan, Metal/Objective-C++, CMake/Ninja, Xcode, Tracy conforme perfil.

**Spec:** `C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-22-linux-vulkan-design.md` e `C:\dev\helworks\helengine\docs\superpowers\specs\2026-10-22-apple-metal-design.md`.

## Global Constraints

- Execução em paralelo foi pedida por Helena; alterações diretas na main dos checkouts reais, preservando mudanças existentes. Sem worktree/export.
- Backend Linux: `C:\dev\helworks\helengine-linux`; backend Apple: `C:\dev\helworks\helengine-apple`. Mac: `distro@192.168.15.7`; Linux: `helena@192.168.15.12`.
- Temporários e outputs fixos por plataforma em `C:\temp\helengine\<plataforma>` ou `/tmp/helengine/<plataforma>`. Sem histórico.
- Sem controle de desktop/screenshots. Compilar e executar verificações offscreen por SSH.
- Política numérica, layouts GPU, manifests e IDs são contratos; não corrigir código gerado posteriormente.
- Arquitetura aprovada: Linux SDL3/Vulkan e Apple SDL3/Metal com dois targets; escolhas arquiteturais adicionais materiais devem voltar à Helena.

## Review Focus

- Paths com espaços e Unicode: mesmos argumentos e arquivos devem chegar ao compilador.
- Inicialização parcial/GPU ausente: erro contextual e liberação de recursos, sem sucesso falso.
- Lifecycle/resize/drawable zero: não apresentar recursos inválidos nem consumir CPU em busy loop.
- Código gerado/conteúdo não disponível: falha explícita; diagnóstico nativo não se passa por build do DemoDisc.
- Políticas e manifests incompatíveis: rejeição antes de linkar/cook, sem defaults silenciosos.

### Task 1: Siblings e toolchains reais

**Files:** novos `AGENTS.md`, `.gitignore`, `README.md`, `CMakeLists.txt` e locks nos dois siblings; catálogo `scripts/workspace-repositories.json` no engine após remotos existirem.
**Interfaces:** executáveis `helengine_linux` e `helengine_macos`; targets `linux`, `macos`, `ios`. CMake recebe diretórios externos de core gerado/conteúdo; não cria cópias de engine.

- [x] Inicializar ambos na main; verificar SSH, SDK e espaço nos hosts sem alterar GUI.
- [x] Fixar dependências SDL3 e SDKs efetivamente usados em lock e documentar comandos reproduzíveis.
- [x] Testar configuração com dependência/core ausente: erro concreto, sem um executável vazio anunciado como player.

### Task 2: Host Vulkan Linux (worker Linux)

**Files:** `C:\dev\helworks\helengine-linux\src`, `tests`, `CMakeLists.txt`, scripts e builder pertencem ao worker Linux.
**Interfaces:** SDL3 recebe eventos e cria surface Vulkan; adapter implementa os contratos gerados de RenderManager/input/Core observados no backend existente. Renderer não altera simulação ou código gerado.

- [x] Fixar testes de seleção/capacidades, falhas de inicialização e resize/minimização.
- [x] Implementar ownership de instance/device/filas/swapchain e submissão sincronizada.
- [x] Compilar pelo checkout real no servidor e validar GPU/offscreen sem abrir desktop da usuária.
- [ ] Implementar fila 2D e integração do core/menu; avançar para materiais, texturas e fila 3D exigidos pelas cenas.
- [ ] Validar os perfis e registrar separadamente host mínimo, menu e cada cena; comunicar bloqueios de integração imediatamente.

### Task 3: Host Metal Apple (worker Apple)

**Files:** `C:\dev\helworks\helengine-apple\src`, `tests`, `CMakeLists.txt`, scripts e builder pertencem ao worker Apple.
**Interfaces:** um renderer Metal, hosts `macos`/`ios`, SDL view/CAMetalLayer; adapter do core gerado e formato de conteúdo obedecem ao engine.

- [x] Fixar testes de lifecycle/drawable indisponível, ownership GPU e paths de recursos/sandbox.
- [x] Implementar command queue/pipelines/buffers/texturas e teste real de render Metal offscreen com readback.
- [x] Compilar no Mac real; verificar SDK iOS/device/simulator e build unsigned sem provisioning inventado.
- [ ] Integrar core/menu, fila 2D, materiais e 3D; registrar limites por target em vez de afirmar paridade sem teste.

### Task 4: Geração e contratos de targets (worker codegen)

**Files:** código de configuração/seleção de plataforma e testes no conversor/engine estritamente necessários; não tocar host CLI/importadores ou siblings dos outros workers.
**Interfaces:** IDs `linux`, `macos`, `ios`; saída C++/ABI existente, política numérica selecionada, providers definidos pelo builder.

- [x] Identificar guards/switches/contratos que atualmente só reconhecem plataformas existentes.
- [x] Testar geração mínima para os três targets e falhas de configuração incompatível.
- [x] Corrigir a geração na origem; comunicar aos workers os nomes de contrato e flags exatos.

### Task 5: Host de build portátil (agente principal)

**Files:** novo CLI no engine, importadores portáveis necessários, scripts `build-platform` e testes correspondentes. Preservar a interface Windows existente.
**Interfaces:** `--project`, `--build`, `--build-profile`, `--output`; reaproveitar manifests, providers, catálogos e pipeline existente sem inicializar WinForms/DirectX no CLI portátil.

- [x] Reproduzir a dependência Windows com o grafo real e teste de execução Linux/Mac.
- [x] Separar inicialização headless de renderer/GUI e registrar importadores compatíveis com os assets reais.
- [x] Resolver binários/ferramentas por host sem `.exe` fixo; falhas continuam fatais e contextualizadas.
- [ ] Buildar conteúdo e código por target usando o DemoDisc real; preservar todos os botões e seleção de cenas. Linux e macOS já geraram e cozinharam as 15 cenas; falta concluir o pacote completo iOS e a validação 3D.

### Task 6: Integração e revisão

**Files:** fixtures/testes de cada dono, catálogo e documentação de uso; sem testes artificiais que apenas espelham implementação.

- [ ] Revisar mudanças por responsabilidade e executar verificações relevantes após cada integração.
- [ ] Verificar hashes/sincronização dos checkouts reais, outputs fixos e ausência de temporários abandonados.
- [ ] Executar menu e matriz de cenas onde o runtime estiver pronto; classificar explicitamente compilado, offscreen, interativo e não validado.
- [ ] Não declarar a criação do backend como paridade completa antes das evidências exigidas pelas specs.
