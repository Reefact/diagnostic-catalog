# ADR-0024 | Échouer sur tout diagnostic que le cliquet d'avertissements ne voit pas

🌍 **Langues :**  
🇬🇧 [English](./0024-fail-on-any-diagnostic-the-ratchet-cannot-see.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-01
**Accepted:** 2026-08-01
**Decision Makers:** Reefact

## Context

Le cliquet d'avertissements de ce dépôt promeut chaque avertissement du compilateur en erreur en CI,
si bien qu'un nouvel avertissement ne peut jamais être fusionné. ADR-0021 y a fait entrer les règles
Sonar C#, en générant le jeu de règles appliqué depuis le profil qualité du serveur.

Cela a refermé un écart et en a laissé un autre ouvert, noté à l'époque et observé depuis. Le cliquet
agit sur les **avertissements**. Un diagnostic d'analyseur Roslyn signalé en dessous de la gravité
avertissement — le SDK .NET signale beaucoup de ses propres règles en `info` par défaut — n'est pas
un avertissement : `dotnet build` n'en imprime rien à aucune verbosité, et le cliquet n'a rien à
promouvoir. SonarQube Cloud l'importe malgré tout, parce que son scanner lit ce que le compilateur a
signalé plutôt que ce que la console a montré.

Mesuré sur ce dépôt : une build signalant **23 diagnostics en `info`** s'est terminée avec zéro
avertissement et zéro erreur, et le tableau de bord listait **23 problèmes**, règle pour règle. Ils
sont arrivés par des pull requests ordinaires dont l'auteur n'avait aucun signal — l'une de ces pull
requests portait un commit affirmant qu'elle tenait ses tests aux règles que `main` applique, ce qui
était vrai des règles Sonar et faux de celles-ci.

Les règles concernées sont activées par le SDK, pas par ce dépôt. Quelles règles ce sont, et à
quelle gravité, bouge à chaque version du SDK.

Deux mécanismes ont été mesurés avant cette décision. Activer chaque règle d'analyseur
(`dotnet_analyzer_diagnostic.severity = warning`) signale **1065 sites**, la plupart venant de règles
que le SDK laisse délibérément éteintes — dont 698 d'une règle de nommage que la convention de
nommage des tests de ce dépôt contredit exprès. Énumérer à la main les règles qui fuient est possible
aujourd'hui, à trois règles, mais rien n'énumère le jeu par défaut du SDK comme un profil qualité
énumère celui de Sonar ; une telle liste est donc un instantané qui pourrit à la prochaine version du
SDK.

## Decision

La build échoue lorsqu'elle signale un diagnostic d'analyseur non supprimé en dessous de la gravité
avertissement.

## Rationale

La propriété qui a échoué n'est pas « une règle était éteinte ». C'est que **la build a signalé
quelque chose sur quoi elle ne pouvait pas échouer** — et un signalement sur lequel personne ne peut
agir est exactement ce que le cliquet existe pour abolir. Énoncer l'invariant, c'est donc énoncer
l'exigence réelle, là où une liste de règles n'énumère que ses instances du jour.

Cela survit aussi au SDK. Une règle qu'une future version se met à signaler en `info` est attrapée la
première fois qu'elle se déclenche, nommée avec son fichier, sa ligne et son message, et doit
recevoir une réponse avant la fusion — alors qu'une liste la laisserait passer en silence, ce qui est
précisément la défaillance qu'on corrige. C'est le même raisonnement qu'ADR-0021 employait pour
générer le jeu de règles Sonar plutôt que l'écrire ; la différence est que le jeu de Sonar est
énumérable et celui du SDK non, la vérification tient donc ici l'invariant plutôt que l'appartenance.

Trois réponses sont admises, et ce sont celles déjà employées ailleurs dans ce dépôt : solder la
violation, relever la règle pour que le cliquet s'en saisisse, ou la supprimer au site avec une
raison. Les trois sont visibles dans l'arborescence, ce qui conserve la propriété qu'ADR-0021 a
établie — une règle est soit appliquée, soit son exception est écrite, jamais discrètement absente.

Les diagnostics supprimés sont ignorés plutôt que signalés, et ce n'est pas une échappatoire. Un
pragma ou un `SuppressMessage` est une décision enregistrée au site, ce qui est la troisième
réponse ; le compilateur les marque dans son journal, on les distingue donc par lecture plutôt que
par supposition.

Relever toutes les règles a été rejeté sur mesure plutôt que par principe. À 1065 sites, dont la
plupart viennent de règles que le SDK choisit de ne pas exécuter, le changement ne serait pas une
application mais un jeu de règles différent — que personne n'a choisi, et qui contredit des
conventions que ce dépôt tient délibérément.

Le coût accepté est une seconde build en CI. Le compilateur n'écrit le journal de diagnostics que si
on le lui demande, et le lui demander change ce que chaque projet émet ; la vérification ne peut donc
pas partager la build de matrice sans altérer la chose même que cette build mesure.

## Alternatives Considered

### Relever chaque règle d'analyseur en avertissement

Envisagé parce que c'est une ligne dans `.editorconfig` et que cela n'exige ni outillage ni
vérification.

Rejeté sur mesure : 1065 sites, dominés par des règles que le SDK laisse éteintes par défaut. Cela
imposerait un jeu de règles que personne n'a sélectionné — dont une qui renommerait chaque test du
dépôt — et le travail de le solder n'a rien à voir avec la fuite qu'on referme.

### Ne relever que les règles connues pour fuir

Envisagé parce que c'est précis, que cela n'exige aucun nouvel outillage, et que cela rend le retour
immédiat et local plutôt que différé à une vérification.

Rejeté comme insuffisant en soi : la liste est un instantané de ce que le SDK actuel signale, et la
version suivante peut y ajouter en silence — ce qui est le mode de défaillance exact que cette ADR
existe pour supprimer. Adopté en complément plutôt qu'en mécanisme : les trois règles connues pour
fuir aujourd'hui sont relevées, pour qu'un contributeur les rencontre dans sa propre build, et la
vérification demeure pour ce que personne n'a encore listé.

### Faire cesser leur import par SonarQube Cloud

Envisagé parce que la fuite n'est visible que parce que le scanner importe les problèmes Roslyn
externes, et que cet import peut être désactivé.

Rejeté parce que cela traite le rapport plutôt que la cause. Les diagnostics seraient toujours
produits et toujours illisibles pour quiconque construit, et le dépôt aurait choisi d'en voir moins
plutôt que d'agir sur davantage.

## Consequences

### Positive

* Un diagnostic qui atteindrait le tableau de bord fait désormais échouer la pull request qui
  l'écrit.
* Le garde-fou est énoncé en propriété ; une règle qu'un futur SDK signalerait en `info` est donc
  attrapée la première fois qu'elle se déclenche plutôt qu'après accumulation.
* Chaque diagnostic du dépôt est désormais soit en échec, soit appliqué, soit supprimé avec une
  raison. Il ne reste aucun troisième état.

### Negative

* Une seconde build en CI, sur un runner.
* Une règle relevée dans `.editorconfig` peut désormais faire échouer une build pour quelque chose
  que le SDK considère comme indicatif, et la solder est un travail que le SDK n'a pas demandé.

### Risks

* La vérification lit le journal de diagnostics du compilateur, dont la forme SARIF est un choix du
  SDK. Les deux formes que le SDK émet sont lues, mais une troisième exigerait de mettre la
  vérification à jour — et une vérification qui ne lirait silencieusement rien rapporterait un succès
  à jamais. Atténué en échouant quand aucun journal n'est trouvé.
* La suppression est la moins coûteuse des trois réponses et pourrait devenir celle par défaut. Rien
  ici ne l'empêche ; la raison écrite au site est ce qu'un relecteur lit.

## Follow-up Actions

* Examiner si la vérification devrait aussi lire la build propre au scanner Sonar, qui tourne avec
  le cliquet désactivé et qui est la seule analyse que ce garde-fou ne couvre pas.

## References

* [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.fr.md) — la décision que
  celle-ci complète ; elle a fait entrer les règles Sonar dans le cliquet et a laissé cet écart de
  gravité ouvert.
* `tools/analysis/check-diagnostic-floor.sh` — la vérification, et les mesures derrière elle.
* `Directory.Build.props` — le cliquet, et le journal de diagnostics que celle-ci lit.
* `.editorconfig` — les trois règles connues pour fuir aujourd'hui, relevées pour que le cliquet
  s'en saisisse.
