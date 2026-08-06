# Configuration

🌍 **Langues :**  
🇬🇧 [English](./configuration.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque règle ce que les analyseurs signalent dans son build. Tous les boutons qui existent —
il y en a moins qu'on ne s'y attend, et c'est voulu.

## Il n'y a pas de format de configuration à nous

Tout ici est standard : les clés `.editorconfig` de Roslyn, et **une** propriété MSBuild. Pas de
`dcat.json`, pas d'attribut à appliquer, et aucune option que les analyseurs liraient en propre. Une
équipe qui sait déjà configurer `CA1822` sait déjà configurer `DCAT0006`.

C'est une décision, pas un oubli. Un format propriétaire serait un fichier de plus à tenir en phase
avec `.editorconfig`, et la première chose qu'il devrait réimplémenter serait le cantonnement par
chemin — que `.editorconfig` fait déjà, et fait mieux.

Cette unique propriété est `EnableDiagnosticCatalogAnalyzers`, et elle existe parce qu'`.editorconfig`
ne peut pas répondre à la question à laquelle elle répond : les analyseurs sont-ils **chargés dans un
projet, tout court**. C'est une autre question que ce qu'ils signalent une fois chargés, et les deux
sont mises côte à côte dans
[Les trois leviers](#les-trois-leviers-et-ce-que-chacun-fait-vraiment), plus bas.

## Gravité, par diagnostic

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion
dotnet_diagnostic.DCAT0013.severity = error
```

Les valeurs acceptées sont celles de Roslyn : `error`, `warning`, `suggestion`, `silent`, `none`,
`default`.

| Identifiant | Sévérité par défaut | Ce qu'une équipe veut d'ordinaire |
| --- | --- | --- |
| `DCAT0001` | **Erreur** | la garder — la paire nomme deux règles différentes, la ligne ne fait donc pas ce qu'elle a l'air de faire |
| `DCAT0002` | **Erreur** | la garder — le type se déclare règle et ne peut pas servir de règle |
| `DCAT0003` | **Erreur** | la garder — sans `Id`, la règle ne publie rien qu'une suppression puisse nommer |
| `DCAT0004` | **Erreur** | la garder — même contrat que `DCAT0003`, l'autre moitié |
| `DCAT0005` | Info | le laisser — il n'y a rien à réparer ; `warning` seulement si vous voulez revoir chacun de ces noms |
| `DCAT0006` | **Erreur** | `suggestion` le temps de migrer un codebase existant, puis retour |
| `DCAT0007` | **Erreur** | la garder — une suppression à moitié migrée est un défaut, pas une tâche de backlog |
| `DCAT0009` | **Erreur** | la garder — le *trimmer* jette purement et simplement cette suppression : la ligne ne fait rien |
| `DCAT0011` | Avertissement | `error` si vous publiez un catalogue — une seule écriture par catégorie, c'est tout l'objet |
| `DCAT0012` | Avertissement | `error` si vous publiez un catalogue — la réparation est mécanique |
| `DCAT0013` | Avertissement | `error` si vous publiez un catalogue et voulez que chaque nom dise sa règle |
| `DCAT0014` | **Erreur** | `suggestion` le temps qu'un codebase existant écrive les raisons qu'il n'a jamais écrites |
| `DCAT0015` | **Erreur** | la garder — livrer un catalogue qui ne vérifie personne est la défaillance qu'il nomme |

**La sévérité dit de quelle sorte de défaut il s'agit, jamais qui lit le message**
([ADR-0040](../adr/0040-grade-every-dcat-diagnostic-by-what-it-says.fr.md)). Une erreur signifie que
le contrat obligatoire n'est pas satisfait, que la suppression est incorrecte ou sans effet, ou que le
paquet ne fournit pas ce qu'il promet. Un avertissement signifie que le code fonctionne aujourd'hui et
reste sujet à dérive (`DCAT0011`, `DCAT0012`) ou trompe qui lit le site d'utilisation (`DCAT0013`).
`DCAT0005` est le seul `Info` : une exception légitime que personne ne peut réparer, signalée pour que
la frontière reste visible.

La distinction qui compte au moment de choisir une ligne à écrire : `DCAT0006` et `DCAT0014` signalent
*du travail pas encore fait*, et tout le reste signale *quelque chose de déjà faux* — sauf `DCAT0005`,
qui signale quelque chose de correct et qui n'aurait pas pu s'écrire autrement. Ce sont ces deux-là
qui ont leur place à `suggestion` pendant un temps. Ils arrivent ensemble, le jour où un codebase
référence un catalogue : `DCAT0006` sur chaque suppression littérale qu'il reconnaît, `DCAT0014` sur
chaque suppression qui n'a jamais porté de raison, littérale ou non. Supprimez chaque ligne quand son
arriéré a disparu.

## Gravité, pour tous d'un coup

Chaque diagnostic `DCAT` est dans la catégorie `DiagnosticCatalog` : le commutateur par catégorie de
Roslyn les atteint donc en groupe.

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
```

Utile comme plancher avec une exception par identifiant par-dessus — la clé par identifiant gagne :

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
dotnet_diagnostic.DCAT0006.severity = suggestion
```

Réglé sur `none`, il fait taire tout diagnostic `DCAT` dans ce projet. Les analyseurs se chargent et
s'exécutent quand même ; rien de ce qu'ils signalent ne survit. C'est l'une des trois façons de ne
plus être dérangé par eux, et les trois ne sont pas interchangeables — voir
[Les trois leviers](#les-trois-leviers-et-ce-que-chacun-fait-vraiment).

## Gravité, par chemin

Les sections d'`.editorconfig` sont des motifs de chemin ordinaires, et la correspondance la plus
spécifique gagne. C'est ainsi qu'une migration avance projet par projet sans jour J :

```ini
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion

[src/Billing/**.cs]
dotnet_diagnostic.DCAT0006.severity = error

[src/Legacy.Interop/**.cs]
dotnet_diagnostic.DCAT0006.severity = none
```

[Adopter un catalogue](adopting-a-catalogue.fr.md) est la stratégie que cela soutient.

## Code généré

**Vous n'avez pas à le configurer, et le défaut n'est pas uniforme.** La vérification est écrite en
deux classes d'analyseur parce que `ConfigureGeneratedCodeAnalysis` est par **analyseur** plutôt que
par diagnostic, et que les deux groupes ont besoin de réglages opposés :

| Analyseur | Diagnostics | Sur le code généré |
| --- | --- | --- |
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`, `DCAT0007`, `DCAT0009`, `DCAT0014` | **non signalés** |
| `DiagnosticRuleDefinitionAnalyzer` | `DCAT0002`–`DCAT0005`, `DCAT0011`–`DCAT0013`, `DCAT0015` | **signalés** |

Une suppression dans un fichier généré n'est pas à l'auteur de la corriger : la signaler noierait
chaque fichier généré sous un travail que personne ne peut faire. Une *déclaration de règle* dans un
fichier généré est le cas inverse : les catalogues que ce dépôt publie sont générés, et les vérifier
est la raison d'être principale de cet analyseur.

Ce que Roslyn compte comme généré, sans que vous disiez rien : un fichier nommé `*.g.cs`,
`*.generated.cs`, `TemporaryGeneratedFile_*.cs`, ou un type portant `[GeneratedCode]`. Pour déclarer
un fichier vous-même :

```ini
[src/Legacy/Interop.cs]
generated_code = true
```

## Références de paquets

Pas de l'`.editorconfig`, mais la configuration que l'on rate le plus souvent.

| Qui vous êtes | Référence | Comment |
| --- | --- | --- |
| Vous écrivez des suppressions | `DiagnosticCatalog.Sonar` (ou un autre catalogue) | référence ordinaire — les vérifications viennent avec |
| Vous voulez les vérifications sans catalogue | `DiagnosticCatalog` | référence ordinaire |
| Vous voulez un catalogue sans l'analyse | ce catalogue | référence ordinaire, plus `EnableDiagnosticCatalogAnalyzers=false` |
| Vous publiez un catalogue | `DiagnosticCatalog` | **référence ordinaire — jamais `PrivateAssets="all"`** — plus le props d'opt-in |
| Vous publiez une bibliothèque qui référence un catalogue | ce catalogue | rien ; vos consommateurs n'en sont pas vérifiés |

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="1.0.0" />
```

Cette unique ligne est tout. Les analyseurs `DCAT` et leurs correctifs sont livrés dans
`DiagnosticCatalog`, dont chaque catalogue dépend : il n'y a donc pas de seconde référence à écrire,
ni de `PrivateAssets` à réussir dessus
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.fr.md)). **Ce ne sont pas un
actif NuGet transitif**, et cette distinction fait toute la section suivante : les assemblages sont
placés là où NuGet ne résout rien, et c'est un fichier de trois lignes embarqué par le catalogue qui
les active pour le projet qui l'a référencé, et pour rien au-delà. Les assemblages d'analyse ne
deviennent jamais une dépendance d'exécution de ce qui vous consomme :
`tools/packaging/verify-consumption.sh` restaure le paquet comme le ferait un consommateur et
asserte qu'ils restent hors du dossier de sortie que `DiagnosticCatalog.dll`, lui, atteint.

`PrivateAssets="all"` sur la **fondation**, depuis un catalogue que vous publiez, est celui à
éviter, et il coûte plus cher qu'avant. Vos consommateurs ne peuvent plus résoudre
`DiagnosticRuleAttribute` dans leur propre source : quiconque déclare ses propres règles obtient
`CS0246` jusqu'à ajouter une dépendance que votre paquet avait déjà — et ils ne sont pas vérifiés
non plus, parce qu'un seul paquet ne fait désormais qu'un seul levier.
[Empaqueter un catalogue](packaging-a-catalogue.fr.md) dit ce qu'un catalogue doit à ses
consommateurs ; le même script mesure les deux moitiés de cette défaillance, la seconde sous
l'intitulé « a catalogue hiding the foundation withholds the attribute assembly ».

**Décliner n'est pas une façon d'être poli** — pour un catalogue. `PrivateAssets="all"` voulait dire
« vérifié par rien » ; il veut dire « ne compile pas », ce que le lecteur reçoit comme un paquet
cassé plutôt que comme un choix que vous avez fait.

## Les trois leviers, et ce que chacun fait vraiment

Trois choses empêchent les diagnostics `DCAT` d'importuner un projet. Ce sont trois comportements
différents, ils ne se remplacent pas, et l'erreur de configuration la plus fréquente consiste à
saisir l'un quand la question en appelait un autre.

| Ce que vous voulez | Ce que vous écrivez | Ce qui se passe vraiment |
| --- | --- | --- |
| cette règle plus discrète, ou ce dossier exempté | `dotnet_diagnostic.DCATxxxx.severity` dans `.editorconfig` | les analyseurs se chargent et s'exécutent ; **ce diagnostic-là** est signalé au niveau nommé, par chemin |
| plus rien de cette bibliothèque signalé ici | `dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = none` | les analyseurs se chargent et s'exécutent ; **tout ce qu'ils signalent** est jeté |
| que les analyseurs ne tournent pas du tout dans ce projet | `<EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>` | les assemblages d'analyse ne sont **pas chargés** ; les constantes du catalogue et `[DiagnosticRule]` arrivent toujours |
| les analyseurs là où un catalogue n'est atteint qu'à travers une bibliothèque | `<EnableDiagnosticCatalogAnalyzers>true</EnableDiagnosticCatalogAnalyzers>` | les analyseurs sont chargés dans un projet que le défaut aurait laissé tranquille |

**Les clés `.editorconfig` gouvernent ce qui est signalé. La propriété gouverne ce qui est chargé.**
Les deux premières lignes ne diffèrent l'une de l'autre que par l'ampleur ; la troisième diffère par
nature. Un projet qui règle la catégorie sur `none` paie encore le chargement et l'exécution des
analyseurs, les voit encore dans la liste d'analyseurs d'un IDE, et les rallume en supprimant une
ligne quelque part dans sa chaîne d'`.editorconfig`. Un projet qui pose la propriété à `false` n'a
aucun analyseur `DCAT` dans la compilation.

Le choix en découle. Faire taire une catégorie est la bonne réponse le temps d'une migration, ou là
où une règle ne s'applique vraiment pas. Décliner le chargement est la bonne réponse quand un projet
veut les constantes d'un catalogue et a décidé, par politique, que cette bibliothèque ne l'analyse pas
— un projet de code généré, un arbre vendorisé, un build appartenant à quelqu'un d'autre.

**Le défaut répond déjà à la question qu'un auteur de bibliothèque poserait.** Depuis
l'[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.fr.md), les
analyseurs atteignent le projet qui a référencé un catalogue et s'y arrêtent : une **bibliothèque**
n'a donc besoin d'aucun levier, puisqu'une application qui la référence n'est pas analysée par un
catalogue qu'elle n'a jamais choisi, et son auteur n'écrit rien pour l'obtenir. Ce qu'apporte la
propriété, ce sont les deux exceptions à ce défaut — un consommateur direct qui décline, et un projet
plus loin qui demande.

```xml
<PropertyGroup>
  <!-- garder le catalogue, décliner l'analyse ; [DiagnosticRule] résout toujours -->
  <EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>
</PropertyGroup>
```

Les deux sens sont mesurés par `tools/packaging/verify-consumption.sh` — « a direct consumer can opt
OUT » et « a consumer two hops out can opt IN » — de même que le fait que se retirer conserve
l'assemblage d'attributs. C'est ce dernier point qui fait du refus de chargement une vraie alternative
plutôt que le paquet cassé que produit `PrivateAssets="all"`.

## Ce que coûte le fait d'avoir les analyseurs activés

Un chiffre qui vaut d'être connu, parce qu'il décide si la réponse est « rien ».

`DCAT0006` a besoin de savoir quelles règles existent, ce qui suppose de balayer les métadonnées de
chaque assemblage référencé susceptible d'en porter. Cet index est construit **paresseusement**, au
premier usage. `DCAT0001`, `DCAT0007`, `DCAT0009` et `DCAT0014` résolvent tout depuis l'attribut
qu'ils ont sous les yeux et n'y touchent jamais — pour `DCAT0007` la règle est nommée par l'argument
déjà migré, ce qui est précisément ce qui rend sa correction pleinement déterministe.

La conséquence : **un projet dont les suppressions sont déjà des références de catalogue ne paie
jamais le balayage.** Le coût tombe pendant la migration, c'est-à-dire exactement quand il y a
quelque chose à trouver, et disparaît quand il n'y en a plus.

## Ce qui n'est délibérément pas configurable

* **Les règles qu'un catalogue décrit.** C'est généré depuis les descripteurs de l'analyseur, et
  l'éditer à la main est la dérive que la génération existe pour empêcher
  ([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.fr.md)).
* **Le caractère *raisonnable* d'une suppression.** Aucun réglage de gravité ne transforme ceci en un
  jugement sur le fait que faire taire une règle à cet endroit était une bonne idée. `DCAT0014` exige
  qu'une `Justification` soit **présente** ; ce qu'elle dit n'est jamais jugé, et aucune clé ne le
  rend possible.
* **`#pragma warning disable` et les clés de gravité d'`.editorconfig`.** Pas un réglage — une
  limite. Les deux prennent du texte nu hors du modèle de compilation C#, si bien qu'aucune constante
  ne peut jamais y être substituée.

## Où aller ensuite

* [**La garantie d'empreinte nulle**](zero-footprint.fr.md) — ce que tout ceci coûte à l'assemblage
  que vous livrez.
* [**Les diagnostics `DCAT`**](diagnostics.fr.md) — ce que signifie chaque identifiant avant de
  décider de sa gravité.
* [**Adopter un catalogue**](adopting-a-catalogue.fr.md) — la rampe de gravité à laquelle ces clés
  servent.

---

<div align="center">
<a href="./writing-suppressions.fr.md">← Écrire des suppressions que le compilateur vérifie</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./zero-footprint.fr.md">La garantie d'empreinte nulle →</a>
</div>
