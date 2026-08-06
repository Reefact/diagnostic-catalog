# Configuration

🌍 **Langues :**  
🇬🇧 [English](./configuration.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque règle ce que les analyseurs signalent dans son build. Tous les boutons qui existent —
il y en a moins qu'on ne s'y attend, et c'est voulu.

## Il n'y a pas de format de configuration

Tout ici est du Roslyn standard. Pas de `dcat.json`, pas de propriété MSBuild, pas d'attribut à
appliquer, et aucune option que les analyseurs liraient en propre. Une équipe qui sait déjà
configurer `CA1822` sait déjà configurer `DCAT0006`.

C'est une décision, pas un oubli. Un format propriétaire serait un fichier de plus à tenir en phase
avec `.editorconfig`, et la première chose qu'il devrait réimplémenter serait le cantonnement par
chemin — que `.editorconfig` fait déjà, et fait mieux.

## Gravité, par diagnostic

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0009.severity = error
dotnet_diagnostic.DCAT0006.severity = suggestion
```

Les valeurs acceptées sont celles de Roslyn : `error`, `warning`, `suggestion`, `silent`, `none`,
`default`.

| Identifiant | Défaut | Ce qu'une équipe veut d'ordinaire |
| --- | --- | --- |
| `DCAT0001` | **Erreur** | la garder — la paire nomme deux règles différentes, la ligne ne fait donc pas ce qu'elle a l'air de faire |
| `DCAT0002` | Avertissement | `error` si vous publiez un catalogue ; sans objet sinon |
| `DCAT0003` | Avertissement | `error` si vous publiez un catalogue |
| `DCAT0004` | Avertissement | `error` si vous publiez un catalogue |
| `DCAT0005` | Info | le laisser — il n'y a rien à réparer ; `warning` seulement si vous voulez revoir chacun de ces noms |
| `DCAT0006` | **Erreur** | `suggestion` le temps de migrer un codebase existant, puis retour |
| `DCAT0007` | **Erreur** | la garder — une suppression à moitié migrée est un défaut, pas une tâche de backlog |
| `DCAT0009` | Avertissement | `error` — le *trimmer* jette purement et simplement cette suppression |
| `DCAT0011` | Avertissement | `error` si vous publiez un catalogue — une seule écriture par catégorie, c'est tout l'objet |
| `DCAT0012` | Avertissement | `error` si vous publiez un catalogue — la réparation est mécanique |
| `DCAT0013` | Avertissement | `error` si vous publiez un catalogue et voulez que chaque nom dise sa règle |
| `DCAT0014` | Avertissement | `suggestion` le temps qu'un codebase existant rattrape, puis `error` |
| `DCAT0015` | Avertissement | `error` si vous publiez un catalogue — en livrer un qui ne vérifie personne est la défaillance qu'il nomme |

La distinction qui compte au moment de choisir : `DCAT0006` et `DCAT0014` signalent *du travail pas
encore fait*, et les autres signalent *quelque chose de déjà faux* — sauf `DCAT0005`, qui signale
quelque chose de correct et qui n'aurait pas pu s'écrire autrement. Ce sont ces deux-là qui ont leur
place à `suggestion` pendant un temps, et d'ordinaire les deux y sont. Ils arrivent ensemble, le jour
où un codebase référence le paquet : `DCAT0006` transforme en erreurs de build toutes les suppressions
littérales qu'il reconnaît, et `DCAT0014` signale toutes celles qui n'ont jamais porté de raison,
littérales ou non. Supprimez chaque ligne quand son arriéré a disparu.

Trois des cinq défauts côté usage sont des erreurs à dessein. Un codebase où la moitié des
suppressions sont des chaînes magiques n'a pas la garantie que cette bibliothèque existe pour
fournir ; il l'a là où quelqu'un y a pensé. Un avertissement laisserait cela à la mémoire. Les deux
autres disent quelque chose de plus étroit — une suppression que le *trimmer* jette (`DCAT0009`), et
une qui ne dit jamais pourquoi elle est là (`DCAT0014`) — et toutes deux signalent des lignes qui se
résolvent correctement, d'où leur arrivée plus discrète et l'intérêt de les relever une fois votre
codebase propre.

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

Le commutateur par catégorie est aussi la réponse pour qui veut les constantes d'un catalogue et
aucun de ses diagnostics. Puisque la vérification est livrée dans le paquet dont chaque catalogue
dépend, aucun agencement de références ne donne l'un sans l'autre, et `none` sur la catégorie est ce
qui exprime ce choix.

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
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
```

Cette unique ligne est tout. Les analyseurs `DCAT` et leurs correctifs sont livrés dans
`DiagnosticCatalog`, dont chaque catalogue dépend : il n'y a donc pas de seconde référence à écrire,
ni de `PrivateAssets` à réussir dessus
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.fr.md)). Les assemblages
d'analyse ne deviennent toujours pas une dépendance d'exécution de ce qui vous consomme :
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

**La propriété est le levier, et il appartient au consommateur.** Depuis
l'[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.fr.md), les
analyseurs atteignent le projet qui a référencé un catalogue et s'y arrêtent : une **bibliothèque**
n'a donc besoin d'aucun levier, puisqu'une application qui la référence n'est pas analysée par un
catalogue qu'elle n'a jamais choisi. Ce qu'apporte `EnableDiagnosticCatalogAnalyzers`, ce sont les
deux exceptions, et c'est une simple propriété MSBuild :

```xml
<PropertyGroup>
  <!-- garder le catalogue, décliner l'analyse ; [DiagnosticRule] résout toujours -->
  <EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>
</PropertyGroup>
```

Posez-la à `true` et un projet est vérifié par un catalogue qu'il n'atteint qu'à travers une
bibliothèque. Les deux sens sont mesurés — « a direct consumer can opt OUT » et « a consumer two
hops out can opt IN » — de même que le fait que se retirer conserve l'assemblage d'attributs, ce qui
en fait une vraie alternative à faire taire `DCAT0006` dans l'`.editorconfig`.

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
  jugement sur le fait que faire taire une règle à cet endroit était une bonne idée. `Justification`
  est là pour cela.
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
