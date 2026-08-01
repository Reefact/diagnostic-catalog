# ADR-0021 | Dériver le jeu de règles Sonar du build depuis le profil qualité du serveur

🌍 **Langues :**  
🇬🇧 [English](./0021-derive-the-build-rule-set-from-the-quality-profile.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Ce dépôt applique un cliquet d'avertissements : la base de code se construit avec zéro
avertissement, et la CI transforme chaque avertissement en erreur, si bien qu'un nouveau ne peut
jamais être fusionné. Le cliquet est énoncé dans `Directory.Build.props` et son raisonnement est
réénoncé dans `.editorconfig` — une règle sur laquelle rien n'agit est la façon dont une règle
dérive.

Les règles Sonar C# se tenaient en dehors. Elles n'étaient évaluées qu'à un seul endroit : la
compilation branchée au scanner dans `.github/workflows/sonar.yml`, qui est aussi la seule build de
ce dépôt où le cliquet est délibérément désactivé, parce que le scanner a besoin que la compilation
se termine pour collecter les diagnostics et les téléverser. Un contributeur — humain ou agent —
rencontrait donc une règle Sonar après la fusion, sur un tableau de bord, et jamais pendant qu'il
écrivait le code. Quarante-six problèmes se sont accumulés ainsi avant que quiconque regarde.

Les règles contre lesquelles le rapport est noté vivent sur le serveur, dans un profil qualité. Le
dépôt ne détenait aucun enregistrement de ces règles.

Le paquet NuGet `SonarAnalyzer.CSharp` n'est pas ce profil. Mesuré ici : sa configuration par défaut
laisse `S3776` désactivée alors que le profil l'active, si bien que les quatre méthodes que le
tableau de bord signalait au-dessus du seuil de complexité cognitive ne produisaient aucun
diagnostic local jusqu'à ce que la règle reçoive une gravité explicite. La divergence joue aussi
dans l'autre sens — le paquet, à sa version actuelle, signale des règles que la dernière analyse
serveur ne signalait pas.

Le profil qualité lié à ce projet est le « Sonar way » intégré de SonarSource : 377 règles actives
énumérables, non modifiables par cette organisation. Il bouge quand SonarSource livre une version
d'analyseur, quelques fois par an.

Séparément, `dotnet-sonarscanner end` téléverse une analyse et rend la main. Il n'attend pas la
barrière qualité et ne la lit pas, et aucun job ne porte le verdict ; `sonar.yml` rapporte donc un
succès dès que le téléversement réussit. Au moment d'écrire ces lignes la barrière est rouge — sur
`new_coverage` seule, chaque condition de problèmes étant verte.

## Decision

L'ensemble des règles Sonar que la build applique est généré depuis le profil qualité SonarQube
Cloud du projet et commité, chaque exception étant écrite dans `.editorconfig`.

## Rationale

Le cliquet existe déjà et fonctionne déjà ; la seule raison pour laquelle les règles Sonar y
échappaient est que la build ne savait pas quelles règles exécuter. Générer cette liste depuis le
profil est ce qui referme l'écart, et cela le referme à l'endroit où le dépôt met déjà de telles
règles — la build qu'un contributeur exécute — plutôt qu'en ajoutant une habitude de revue.

Lire le profil plutôt que se fier aux défauts du paquet n'est pas un raffinement, c'est tout
l'objet. Les deux divergent dans les deux sens, et le cas mesuré de `S3776` montre le sens qui
compte : une règle que le rapport applique et que la build ignore produit exactement le silence que
cette décision existe pour supprimer. Une liste générée est aussi la seule forme dont on puisse
vérifier la dérive ; une liste écrite à la main pourrirait dès la première version livrée par
SonarSource, et personne ne le saurait.

L'application par défaut découle du fait que l'alternative a été mesurée et ne fonctionne pas. En
`suggestion`, un diagnostic Sonar n'imprime rien dans `dotnet build` à aucune verbosité — il atteint
un IDE et un fichier de log et personne d'autre — une liste générée à cette gravité aurait donc été
invisible précisément pour le lecteur pour qui elle existe.

Les exceptions vivent dans `.editorconfig` plutôt que dans le fichier généré, pour que
l'appartenance reste générée et que chaque écart reste écrit à la main et argumenté. Deux sortes
sont admises, et la distinction est délibérée : une règle dont les violations ne sont pas encore
soldées porte son décompte et dit « pas encore », tandis qu'une règle que cette base de code refuse
porte sa raison. Une règle mise de côté est une dette avec un nom, pas une règle discrètement
éteinte. Il s'ensuit que la mise de côté est temporaire par construction — le meilleur état final
pour un petit ensemble délibéré est une suppression au site, qui garde la règle appliquée partout
ailleurs.

Régénérer la liste peut faire virer la build au rouge, et c'est accepté plutôt qu'atténué : une
règle que le profil ajoute doit être soldée ou mise de côté délibérément, ce qui est le même marché
que le cliquet d'avertissements passe déjà.

La barrière est lue selon un calendrier plutôt qu'attendue dans la pull request, parce que les deux
questions sont différentes. Faire attendre le scanner coupleraît la possibilité de fusionner à un
service tiers — l'analyse tourne à chaque pull request, une panne arrêterait donc chaque fusion,
alors qu'une barrière rouge aujourd'hui n'arrête rien. Une lecture planifiée applique le verdict et
coûte une nuit rouge au lieu d'un dépôt gelé. Elle n'est pas non plus redondante avec la build : la
barrière mesure les règles d'exécution symbolique que le paquet d'analyseur n'exécute pas, chaque
famille de règles non C#, ainsi que la couverture, la duplication et la revue des points chauds, ce
à quoi aucun analyseur ne peut répondre. La condition rouge aujourd'hui relève de cette dernière
classe.

Le coût accepté est la dérive de versions. L'épinglage du paquet suit la ligne de release de
SonarSource tandis que le profil suit celle du serveur ; une montée peut donc introduire une règle
que le rapport n'a pas encore. C'est la même forme que les montées d'analyseurs que ce dépôt prend
déjà, et la vérification hebdomadaire de dérive est ce qui garde les deux visibles l'une à l'autre
plutôt que silencieusement séparées.

## Alternatives Considered

### Laisser les règles Sonar au tableau de bord

Envisagé parce que c'est le statu quo et que le garder ne coûte rien : l'analyse tourne déjà, et les
problèmes sont déjà listés.

Rejeté parce que la preuve contraire est l'histoire propre à ce dépôt. Quarante-six problèmes ont
atteint `main` sous ce régime, dont quatre méthodes au-dessus d'un seuil de complexité, et aucun n'a
été vu par la personne qui écrivait le code au moment où elle l'écrivait. Un signal qui arrive après
la fusion est le mode de défaillance que le cliquet a été construit pour empêcher.

### Ajouter le paquet d'analyseur et accepter son jeu de règles par défaut

Envisagé parce que c'est une ligne et que cela n'exige ni outillage, ni fichier généré, ni job
planifié.

Rejeté sur mesure : les défauts du paquet laissent `S3776` éteinte ; les constats les plus
importants que le tableau de bord signalait auraient donc encore été fusionnés sans être vus, et la
build et le rapport auraient continué de diverger sur les règles qui existent — la défaillance la
plus dure, parce qu'elle ressemble à un accord.

### Faire attendre le scanner sur la barrière qualité

Envisagé parce que cela appliquerait le verdict à la pull request, là où il est le moins coûteux
d'agir.

Rejeté parce que cela couple la possibilité de fusionner à un service tiers : l'analyse tourne à
chaque pull request, une panne de SonarQube Cloud bloquerait donc chaque fusion — une défaillance
strictement pire que celle qu'on corrige, puisqu'une barrière rouge ne bloque rien aujourd'hui.

### Faire réparer le fichier par la vérification de dérive elle-même

Envisagé parce que cela supprime une étape manuelle, et que la régénération est mécanique.

Rejeté parce que cela donnerait à un job planifié un accès en écriture au fichier même qui gouverne
quelles règles bloquent une fusion. Rapporter la dérive et laisser la régénération à un humain garde
cette décision là où les décisions appartiennent. La promouvoir pour qu'elle ouvre une pull request
reste un petit changement si l'échange est un jour jugé rentable.

## Consequences

### Positive

* Une règle Sonar fait désormais échouer la build qui l'introduit, sur la machine du contributeur
  et en CI, au lieu d'apparaître sur un tableau de bord après la fusion.
* La build et le rapport parlent de façon démontrable des mêmes règles, et la vérification de
  dérive maintient cet état.
* Chaque règle non appliquée est nommée, avec un décompte ou une raison. Il n'y a aucune exception
  silencieuse.
* Le verdict de la barrière qualité est enfin appliqué par quelque chose.
* La pull request d'un fork reçoit les règles aussi : le job d'analyse ne peut pas tourner sans un
  secret qu'un fork ne peut pas lire, mais le paquet d'analyseur n'en a besoin d'aucun.

### Negative

* Une nouvelle dépendance, et un coût de compilation sur chaque projet.
* La dérive de versions entre l'épinglage du paquet et l'analyseur du serveur devient une chose à
  gérer.
* Régénérer après un changement de profil peut faire virer la CI au rouge, et le travail de solder
  ou de mettre de côté retombe sur celui qui régénère.

### Risks

* Une montée de paquet peut introduire des règles que le serveur n'a pas encore, faisant virer la
  build au rouge sur des constats dont le tableau de bord ne dit rien. Atténué par la vérification
  de dérive qui rend la différence visible, et par le mécanisme de mise de côté qui lui donne un
  endroit où aller.
* La vérification nocturne de la barrière ne bloque aucune fusion par conception ; elle peut donc
  être ignorée. C'est une alarme, et une alarme à laquelle personne ne répond est le mode de
  défaillance contre lequel cette ADR met en garde ailleurs.

## Follow-up Actions

* Solder les 18 sites `S8969` mis de côté et supprimer l'entrée.
* Remplacer les deux refus (`S101`, `S6562`) par des suppressions aux sites, pour que les règles
  restent appliquées partout ailleurs.
* Décider si la condition `new_coverage` de la barrière qualité est satisfaite en relevant la
  couverture ou en déplaçant le seuil. Cette ADR applique le verdict ; elle ne le tranche pas.

## References

* `Directory.Build.props` — le cliquet d'avertissements, et où l'analyseur et le jeu de règles
  généré sont branchés.
* `build/sonar-profile.globalconfig` — le jeu de règles généré.
* `.editorconfig` — les exceptions, et le seul endroit où une règle n'est pas appliquée.
* `tools/sonar-profile/` — le générateur et le lecteur de barrière.
* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) — le même argument
  pour les règles de code : une règle énoncée là où aucun compilateur et aucun agent ne peut la lire
  n'est appliquée par personne.
* [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.fr.md) — pourquoi les deux nouveaux
  scripts sont en `sh` POSIX.
* `Reefact/first-class-errors`, ADR-0062 — le dépôt frère où cet arrangement a été construit en
  premier ; cette ADR l'adopte, avec les mesures reprises sur ce dépôt.
