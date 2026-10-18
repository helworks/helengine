# Processamento de layout do viewport por plataforma

Data: 2026-10-18
Estado: proposta escrita para revisão. Os três modos e a escolha por viewport/plataforma foram aprovados na conversa; implementação ainda não iniciada.

## Objetivo e restrições

Entregar layout já resolvido no cook quando o destino tem geometria de apresentação conhecida. Permitir processamento único no carregamento e recomposição dinâmica quando esses comportamentos forem desejados.

- PS1 pode usar layout preparado no cook sem manter uma segunda cópia dos valores de UI.
- Uma plataforma com pouca RAM também pode escolher processamento único durante o carregamento, sem criar snapshots temporários para depois descartá-los.
- O cache dinâmico é opcional. Seus dados são específicos dos componentes presentes; desaparece o registro universal por entidade.
- A política é uma propriedade do viewport, selecionada pelo mecanismo existente de overrides de propriedade por plataforma.
- Trabalhar nos checkouts reais e manter o estado local da tarefa anterior. Nenhum worktree, export ou histórico de builds.
- Esta mudança não cria um backend de SNES.

## Contrato público proposto

Adicionar a propriedade persistida **LayoutProcessingMode**, com enum de representação byte:

| Valor | Modo | Execução do layout | Retenção de valores originais no player |
| --- | --- | --- | --- |
| 0 | Dynamic | Preparação e alterações relevantes de layout | Cache compacto por componente |
| 1 | OnLoad | Uma passagem durante a preparação da cena | Nenhuma |
| 2 | Cook | Passe de cook antes da gravação da cena empacotada | Nenhuma |

ScalingMode, binding, dimensões de referência, anchors, escala de UI e tratamento de fontes continuam definindo o resultado visual. A nova propriedade define quando executar esse trabalho.

Cenas antigas e componentes sem a nova propriedade assumem Dynamic, preservando o comportamento durante a migração. Isso não significa escolher Dynamic para todos os destinos novos: o viewport pode ter Common e overrides diferentes.

A política é resolvida antes da preparação. Trocar de política depois de uma instância estar preparada exige recarregar a cena. Não há conversão implícita entre estados já processados.

As propriedades atuais de tamanho, posição e estilo mantém sua semântica de runtime nesta entrega: o processamento pode escrever valores resolvidos nelas. Não se introduz uma mudança geral de API em que todo getter passe a significar valor autoral.

## Contexto gráfico efetivo

O cook recebe as métricas efetivas selecionadas pelo backend para aquele viewport:

- tamanho real da superfície/retângulo alvo;
- pixel aspect do modo de vídeo;
- binding e retângulo da câmera, quando aplicáveis;
- DPI/densidade e escala de UI, se o modo de scaling depender deles.

Os valores genéricos do documento de projeto não são uma autoridade suficiente quando o backend seleciona outro modo de vídeo.

Se o alvo não puder ser determinado estaticamente, Cook deve produzir um erro com o viewport e o parâmetro não resolvido. Não aplicar uma resolução de fallback silenciosa nem trocar automaticamente para outro modo. OnLoad e Dynamic são escolhas explícitas para alvos dependentes do runtime.

Para Cook, a cena empacotada contém apenas metadados compartilhados suficientes para identificar o alvo e inicializar seus providers de bounds/escala. Não contém os valores originais de cada entidade.

Uma instância cooked é destinada ao contexto preparado. Recarregar o mesmo asset cooked não o adapta a uma resolução diferente. Geração de várias variantes de uma cena por resolução não faz parte desta entrega.

## Passe de cook

O passe opera na representação de cena usada pelo empacotador, depois da resolução dos overrides efetivos e da expansão/pruning de blueprints. Não executa callbacks de gameplay e não altera os arquivos autorais.

O fluxo deve garantir:

1. Resolver a política e as métricas efetivas de cada owner de layout.
2. Aplicar os mesmos cálculos usados pelos modos de runtime aos participantes do owner.
3. Persistir os valores resolvidos apenas no artefato empacotado.
4. Marcar esse owner como já processado para que o carregamento não aplique o fit novamente.
5. Inicializar no player os bounds e fatores compartilhados sem criar um cache de valores originais.

Resolução, pixel aspect, parâmetros relevantes de densidade/escala, política efetiva e versão do algoritmo participam da chave do cook. Alterar um deles invalida o artefato, mesmo que o nome do perfil gráfico permaneça igual.

O modo NoScaling continua sendo uma operação identidade. Um viewport que apenas fornece bounds ou roteamento de câmera continua disponível mesmo quando não executa fit em runtime.

## Processamento ao carregar

OnLoad percorre a hierarquia preparada uma vez e aplica o layout diretamente aos valores carregados. Não cria ViewportLayoutSnapshot, cache de originais ou cópia por entidade para realizar essa passagem.

A fase ResolveLayout e as leituras de bounds não podem reaplicar o fit. Os callbacks ScenePrepared recebem a cena já ajustada, como hoje.

Depois da preparação, mudanças na apresentação não provocam recomposição automática desse owner. A aplicação deve recarregar o mapa para usar novos valores autorais com as novas métricas.

Instâncias novas criadas em runtime precisam receber seu layout uma única vez na sua preparação; não se reaplica o fit sobre todos os elementos já processados.

## Modo dinâmico e propriedade dos dados

O viewport coordena o layout; cada operação de captura/aplicação corresponde a um participante real. Não há espaço reservado para texto em uma entidade que só contém sprite, nem dados de scroll em todos os registros.

A organização proposta usa registros de valor em buffers tipados dimensionados para os participantes, evitando uma alocação por registro. Os registros associam dados originais ao componente que os usa. Posições pertencem ao canal de transform do layout UI, sem adicionar um cache obrigatório a toda Entity da engine.

Conjunto inicial equivalente ao comportamento atual:

- posição local;
- distâncias de anchors;
- tamanho, raio e borda de retângulo;
- tamanho, escala de fonte, outline e sombra de texto;
- tamanho de sprite;
- tamanho de clip e interactable;
- tamanho e extensão de item de scroll;
- escala e margem do overlay de FPS quando ele participar.

Nenhuma segunda cópia dessas propriedades é criada nos modos Cook e OnLoad. A ausência de cache nesses modos é uma condição do caminho de execução, não um cache criado e liberado logo depois.

ViewportLayoutSnapshot e o snapshot universal equivalente de ReferenceCanvasFitComponent deixam de ser o formato de retenção. O fit legado deve usar o mesmo mecanismo de participantes e respeitar a política do owner de viewport, evitando reintroduzir o custo em uma segunda implementação.

O update dinâmico parado não conta entidades nem pesquisa tipos da hierarquia inteira. Adição, remoção, reparenting, mudança de owner/binding e alterações relevantes de propriedades invalidam os owners afetados. As buscas e a reconstrução ocorrem nessas mudanças; resize invalida a aplicação dos valores retidos.

A remoção de um componente retira suas referências antes da destruição nativa. Reconstruir a composição com o mesmo número de entidades não pode manter referências antigas.

Owners aninhados mantém limites explícitos: a mesma propriedade não recebe o mesmo fit duas vezes. Viewports independentes continuam podendo ter políticas diferentes.

## Editor, persistência e geração C++

A política por plataforma é aplicada ao conteúdo empacotado. O editor continua podendo redimensionar sua prévia e persistir os valores autorais.

O contrato atual de salvar/clonar a cena, incluindo edição na prévia escalada, deve ser preservado usando os novos participantes tipados. O editor pode manter o estado adicional necessário é autoria; esse estado não deve ser criado no player Cook/OnLoad.

Usar o registro existente de persistência de componentes e os overrides de propriedade, sem adicionar um dicionário paralelo de plataformas dentro do viewport.

Atualizar os serializers/deserializers oficiais e regenerar os consumidores nativos pelo pipeline normal. Não editar C++ gerado. Manter leitura compatível de componentes antigos pelo mecanismo de versão existente.

Os cálculos de fit/stretch/density, pixel aspect, anchors e fontes em native pixels têm uma implementação compartilhada entre cook e runtime. A representação dos dados pode diferir; os resultados devem ser equivalentes para o mesmo alvo.

## Consumidores do DemoDisc

A aceitação inclui o menu em execução, não apenas o asset imediatamente após o cook:

- rodapé deve usar a escala efetiva sem aplicar uma segunda escala a valores já processados;
- scroll e hit test devem usar o tamanho e o espaçamento efetivos dos itens;
- scripts não devem depender de snapshots para resolver referências;
- animação do logo e outros canais de transform devem compor com o layout preparado sem salto no primeiro update ou alteração global de clips compartilhados;
- UI gerada em runtime, como barras de scroll, deve derivar seu layout dos bounds efetivos e não criar caches universais de forma indireta.

O trabalho de cache já feito no menu/rodapé/scroll deve ser integrado e preservado. A árvore de botões continua completa, independentemente das cenas empacotadas.

## Orçamento de memória

A estimativa levantada para a cena salva atual tem 127 entidades sob o viewport, 47 textos, 54 retângulos, 24 layouts, 2 sprites, 7 clips e 7 scrolls.

No modelo de 32 bits, os registros tipados estimados ocupam 5.128 bytes; se os elementos das sete barras de scroll também forem retidos, 5.828 bytes. Reserva de projeto: aproximadamente 6–7 KiB no modo Dynamic, a confirmar com ABI/alocador e população efetiva.

Cook e OnLoad devem reter zero bytes de cópia de valores originais por entidade/componente. Os próprios componentes, dados de apresentação, metadados do viewport, comandos de desenho e caches já existentes de transform não fazem parte dessa alegação.

O snapshot universal atual mediu 208 bytes em Windows x64 e 160 bytes no modelo de 32 bits; esses valores não são o orçamento do formato novo.

## Verificação necessária

- Persistir/recarregar a nova propriedade e seus overrides por plataforma; validar default de cenas antigas.
- Comparar Cook, OnLoad e Dynamic para o mesmo alvo em fit, stretch, density e pixel aspect não quadrado.
- Confirmar ausência de drift e aplicação única após leituras de bounds e callbacks de preparação.
- Confirmar zero alocações de cache de originais em Cook/OnLoad, inclusive durante a preparação.
- Validar resize dinâmico, criação/remoção de componentes e reparenting com contagem de entidades inalterada.
- Exercitar owners aninhados e o fit legado, incluindo salvar/clonar/editar a prévia sem alterar os arquivos autorais durante cook.
- Invalidar o cache do cook por métricas e versão do algoritmo; comparar builds limpas e incrementais.
- Validar binários gerados e o menu em Windows e PS1; regenerar e recompilar os consumidores afetados do contrato compartilhado.
- Medir RAM retida, pico de preparação e custo de update. As estimativas anteriores não substituem essas medições.

## Limites desta entrega

Não inclui variantes automáticas de cena por resolução, implementação de SNES, mudança geral de semântica dos getters públicos de UI ou refatorações independentes dos renderers. Não altera resoluções de projeto para fazer o cook passar.

O próximo passo, após a revisão desta especificação, é um plano de implementação que distribua as mudanças entre core, persistência, cook, contrato gráfico dos backends e consumidores do DemoDisc.
