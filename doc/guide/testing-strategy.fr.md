# La stratégie de test

🌍 **Langues :**  
🇬🇧 [English](./testing-strategy.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque ajoute un test, ou se demande pourquoi il y a sept projets de test. Chacun existe
parce que quelque chose que les autres ne peuvent pas atteindre échouerait sinon en silence.

Trois autres projets siègent sous [`tests/`](../../tests) et n'assertent rien eux-mêmes :
`DiagnosticCatalog.Usage` est un consommateur dont la suite d'empreinte nulle inspecte la
compilation ([ADR-0030](../adr/0030-keep-the-usage-suite-out-of-the-sonar-analysis.fr.md)), et
`CatalogGen.AbsentContract` et `CatalogGen.PartialLoadFixture` sont des assemblages d'analyseurs
compilés pour que les tests du générateur échouent dessus.

## Les sept projets

| Projet | Asserte | Tourne sur |
| --- | --- | --- |
| `DiagnosticCatalog.UnitTests` | La fondation : que les attributs survivent dans les métadonnées et se relisent | net10.0 **et le CLR .NET Framework 4.7.2** |
| `DiagnosticCatalog.ZeroFootprint.UnitTests` | L'autre moitié : qu'une suppression ne laisse aucune trace dans le build d'un consommateur | net10.0 **et le CLR .NET Framework 4.7.2** |
| `DiagnosticCatalog.Catalogs.UnitTests` | Les catalogues générés, et les documents qui les décrivent | net10.0 **et le CLR .NET Framework 4.7.2** |
| `DiagnosticCatalog.Analyzers.UnitTests` | Chaque diagnostic et chaque correctif | net10.0 |
| `CatalogGen.UnitTests` | Acquisition, lecture de descripteurs, nommage, émission | net10.0 |
| `DiagnosticCatalog.Cli.UnitTests` | L'arbre des commandes, les codes de sortie, ce que chaque verbe refuse | net10.0 |
| `DiagnosticCatalog.Documentation.UnitTests` | La documentation, contre elle-même et contre le code | net10.0 |

Plus une suite que `dotnet test` ne peut pas atteindre — voir
[la suite shell](#la-suite-que-dotnet-test-ne-peut-pas-atteindre).

## Le plancher .NET Framework

Un projet y adhère en important les props partagées et en abandonnant son propre
`<TargetFramework>` :

```xml
<Import Project="..\build\Net472TestFloor.props" />
```

Cet import est toute l'appartenance. Le job CI `framework-floor`, réservé à Windows, découvre chaque
projet qui le porte et exécute chacun avec `-p:EnableNet472Floor=true -f net472`. La build interne est
conditionnée par cette propriété : un `dotnet build` ordinaire — et toute la boucle locale — ne la
voit jamais.

**Quels projets y adhèrent est une décision, pas un défaut.** Un projet de test y adhère quand il
exerce une bibliothèque `netstandard2.0` livrée, parce que c'est ce que le plancher existe pour
prouver : non pas que le code *compile* contre `netstandard2.0`, mais qu'il *tourne* sur le vrai CLR
4.7.2 ([ADR-0001](../adr/0001-floor-the-libraries-on-net-framework-4-7-2.fr.md)).

Les quatre projets qui restent en dehors couvrent de l'outillage qui ne rencontre jamais ce runtime :
`dcat` est planché à net8.0, les analyseurs tournent dans un compilateur hôte, le générateur est de la
compilation, et les tests de documentation lisent du Markdown. Les relancer sur un autre CLR coûterait
des minutes et ne prouverait rien.

## La défaillance contre laquelle chaque couche est écrite

Chaque suite est façonnée par la manière caractéristique dont son type de test pourrit.

**Les tests d'analyseur** échouent en n'exécutant jamais l'analyseur. Il n'a pas été enregistré, son
`SupportedDiagnostics` est vide, ou il a levé et Roslyn a avalé la levée en `AD0001` — et chaque test
« aucun diagnostic attendu » passe alors pour toujours, en devenant plus rassurant à chaque ajout.

`AnalyzerHarness` asserte donc trois choses à **chaque** exécution, avant de regarder la moindre
attente :

* l'analyseur déclare au moins un diagnostic ;
* l'extrait compile — un montage incompilable remet des types d'erreur à l'analyseur et transforme
  chaque attente en étude du néant ;
* rien n'a signalé `AD0001`.

**Les tests négatifs** échouent en n'ayant plus de sujet. `ZeroFootprintTests` asserte qu'une
suppression n'a laissé aucune trace — une assertion qui passerait pour toujours dès le jour où son
sujet cesserait d'être compilé. Le sujet porte donc un attribut marqueur propre au test, et la
première assertion est que *celui-là* a survécu.

**Les théories pilotées par découverte** échouent en ne découvrant rien. `DocumentedSiblingsTests` lit
les catalogues dans `eng/catalogs.json` ; si le manifeste cessait d'être copié à côté des tests, une
famille vide n'asserterait rien du tout. Elle asserte donc que la famille compte au moins deux membres
avant d'asserter quoi que ce soit à son sujet. Chaque suite ici qui découvre ses propres entrées porte
le même garde-fou.

## Ce que couvre chaque suite

### La fondation, et les deux moitiés

`DiagnosticCatalog.UnitTests` et `DiagnosticCatalog.ZeroFootprint.UnitTests` assertent des **choses
opposées sur le même sujet**, et aucune n'a de sens seule : l'une montre que les valeurs sont lisibles
quand un outil les demande, l'autre qu'elles ne coûtent rien au consommateur quand aucun outil ne le
fait.

Le projet zero-footprint ne définit délibérément **pas** `CODE_ANALYSIS` : il compile donc comme le
build d'un consommateur.

### Les catalogues, et les documents à leur sujet

`DiagnosticCatalog.Catalogs.UnitTests` vérifie la sortie générée — chaque règle expose un identifiant
et une catégorie non vides, chaque membre est une constante littérale, les identifiants sont uniques
et correspondent au nom de leur type, chaque catégorie est déclarée par la classe de catégories du
catalogue, aucun conteneur n'est masqué par son espace de noms, et la provenance enregistre la version
amont.

Elle lit aussi les **documents**, parce que rien ne compile un README : que le bandeau miroir de
chaque catalogue corresponde à l'attribut `CatalogSource` que le générateur a écrit, que chacun nomme
ses frères et la fondation, et qu'une adresse nuget.org dans un README résolve vers un paquet que ce
dépôt publie réellement.

### Les diagnostics et les correctifs

`DiagnosticCatalog.Analyzers.UnitTests` couvre chaque identifiant `DCAT`, chaque correctif, et — la
partie à lire avant d'en ajouter un — chaque **refus**. `ADR-0018` demande qu'une affirmation sur ce
qu'un correctif décline soit testable plutôt qu'affirmée : chaque « aucun correctif proposé ici » a
donc un test qui échoue à moins que le diagnostic n'ait quand même été signalé.

`MarkerRecognitionTests` est le plus petit fichier avec le plus d'enjeu : il couvre les deux cas
qu'une comparaison de symboles manquerait en silence — un catalogue déclarant son propre marqueur, et
un consommateur incapable de résoudre la fondation.

### Le générateur

`CatalogGen.UnitTests` couvre l'acquisition depuis chaque type de source, ce que chacune refuse, et les
propriétés d'émission qui rendent une exécution reproductible.

### La documentation

`DiagnosticCatalog.Documentation.UnitTests` lit l'arbre de travail plutôt qu'une copie mise en scène,
parce qu'il suit des liens hors de `doc/` et qu'une copie devrait reproduire la disposition qu'elle
vérifie. La racine du dépôt voyage en métadonnée d'assemblage, estampée à la compilation.

Au-delà de la parité et de la navigation, deux de ses assertions sortent entièrement de la
documentation : chaque `DCAT` livré est documenté et chaque `DCAT` documenté est livré ; chaque option
`dcat` de la référence existe sur les types de configuration de l'outil, et chaque option exposée par
l'outil y figure. Les deux comparent de la prose à la vérité compilée plutôt qu'à un autre document.

## La suite que `dotnet test` ne peut pas atteindre

```bash
sh tools/tests/run.sh
```

Les scripts sous `tools/` décident **de ce qu'une release publie**. `trains.sh` répond aux projets qui
appartiennent à un train, et les scripts d'empaquetage empaquettent exactement ce qu'il rapporte. Un
projet que la découverte manque est silencieusement absent de sa propre release ; un projet qu'elle
trouve à tort est publié alors qu'il ne le devrait pas. Ni l'un ni l'autre n'apparaît en build rouge,
et `dotnet test` ne peut pas atteindre le shell du tout.

Les tests vivent dans `tools/tests/`, un `test-<script>.sh` par script. Chacun s'exécute dans son
propre processus, source `tools/tests/assert.sh`, et **finit par `finish`** — un fichier qui l'oublie
sort sur le statut de sa dernière commande et rapporte un succès quel que soit le nombre d'assertions
échouées.

La suite est invoquée avec `sh` plutôt que `bash` : chaque script porte un shebang `#!/bin/sh` et est
écrit en POSIX ([ADR-0013](../adr/0013-write-the-shell-tooling-for-posix-sh-not-bash.fr.md)), si bien que
l'exécuter sous bash laisserait passer un bashisme en CI pour le faire échouer sur la machine d'un
contributeur.

## Ajouter un test pour un nouveau diagnostic

1. **Écrivez les assertions d'abord**, si le contrat n'est pas évident. Leur valeur est le moment
   qu'elles créent : si vous n'arrivez pas à décider quoi asserter, c'est le point où demander plutôt
   que de trancher la question en silence dans l'implémentation.
2. Ajoutez l'identifiant à `DiagnosticIds` et un descripteur à `Descriptors`. `RS2008` l'exigera dans
   `AnalyzerReleases.Unshipped.md`, qui est aussi là où les tests de documentation lisent l'ensemble
   livré — le guide doit donc gagner une section pour lui dans **les deux** langues, ou le build
   échoue.
3. Placez-le sur l'analyseur dont il a besoin du réglage de code généré. Les diagnostics de site
   d'utilisation vont sur `SuppressionUsageAnalyzer` ; ceux de déclaration sur
   `DiagnosticRuleDefinitionAnalyzer`.
4. Ajoutez des tests à `DiagnosticCatalog.Analyzers.UnitTests` — y compris un pour tout ce que le
   correctif décline de faire.
5. Régénérez `DiagnosticCatalog.Self`, parce que la CI le compare et qu'un nouvel identifiant ne peut
   pas sortir sans le catalogue qui le publie.

## Prouver un correctif

Un `fix` part avec un test **qui a été vu échouer contre le code non corrigé**. Écrivez le test
d'abord, ou écrivez le correctif et remisez-le pour voir le test passer au rouge — l'un ou l'autre
suffit. Un test qui n'a jamais été rouge ne peut pas distinguer un bug corrigé d'un bug jamais
reproduit.

Là où un test qui échoue est réellement impraticable — une course, un correctif dans un workflow, un
défaut atteignable seulement via un service tiers — dites-le dans la pull request et décrivez comment
vous avez vérifié à la place. Sauter la preuve est permis ; la sauter en silence ne l'est pas.

## Où aller ensuite

* [**Architecture du dépôt**](architecture.fr.md) — les quatre couches de vérification indépendantes
  et ce que chacune atteint.
* [**Dans le générateur**](generator-internals.fr.md) — ce sur quoi porte `CatalogGen.UnitTests`.
* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — le plancher, la suite shell, et la convention de
  commit.

---

<div align="center">
<a href="./release-trains.fr.md">← Les trains de release</a> · <a href="./README.fr.md">↑ Table des matières</a>
</div>
