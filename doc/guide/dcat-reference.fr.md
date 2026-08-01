# La référence `dcat`

🌍 **Langues :**  
🇬🇧 [English](./dcat-reference.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque sait déjà ce que fait l'outil et a besoin du comportement exact. Chaque commande,
chaque option, chaque code de sortie. [Le tour d'horizon](dcat.fr.md) est le point de départ si ce
n'est pas votre cas.

Tout ce qui suit est vérifié contre les types de configuration de l'outil par
`tests/DiagnosticCatalog.Documentation.UnitTests` — une option documentée ici que `dcat` ne déclare
pas fait échouer le build, et une option qu'il déclare et que cette page omet aussi.

## Commandes

| Commande | Description |
| --- | --- |
| `dcat generate` | Générer un catalogue depuis un paquet NuGet, un projet, une solution, ou des assemblages d'analyseur sur disque. |
| `dcat validate` | Vérifier qu'un catalogue correspond encore à sa source, sans rien écrire. |
| `dcat list <CATALOGUE>` | Lister les règles qu'un catalogue compilé publie. |
| `dcat explain <CATALOGUE> <RULE-ID>` | Expliquer une règle, et imprimer la suppression qui la référence. |
| `dcat --help` | L'arbre des commandes. Aussi `dcat <commande> --help`. |
| `dcat --version` | Quelle version de l'outil est installée. |

`list` et `explain` prennent le chemin d'un assemblage **compilé**, pas d'un fichier source.

## Nommer une source

`generate` et `validate` ont besoin d'exactement une source. En nommer plusieurs est refusé plutôt que
résolu par priorité — un outil qui choisirait en silence rendrait l'erreur invisible.

| Option | Ce qu'elle nomme |
| --- | --- |
| `--package <ID>` | Le paquet NuGet dont lire les analyseurs. |
| `--package-version <VERSION>` | Quelle release de `--package` : une version exacte, `latest` (dernière **stable**), ou `latest-any` (préversions comprises). |
| `--source <NAME-OR-URL>` | De quel flux configuré lire `--package`. Par défaut, toutes les sources activées de `NuGet.config`. |
| `--nupkg <PATH>` | Un `.nupkg` déjà sur disque. Son `.nuspec` nomme la source sauf indication contraire. |
| `--project <PATH>` | Un projet qui produit des analyseurs, **déjà construit**. Répétez pour en lire plusieurs ensemble. |
| `--solution <PATH>` | Une solution ; lit les projets qui déclarent `ProducesDiagnosticRules`. **Déjà construits.** |
| `--assembly <PATH>` | Un assemblage d'analyseur déjà sur disque. Répétez pour en lire plusieurs ensemble. |
| `--configuration <NAME>` | Quelle configuration de `--project` ou `--solution` lire. Par défaut `Release`. |
| `--language <LANG>` | Les analyseurs de quel langage lire dans un paquet. Seul `cs` est lisible aujourd'hui. |
| `--manifest <PATH>` | Générer chaque catalogue déclaré dans un manifeste. Les chemins qu'il contient sont relatifs à lui. |

**`--package-version`, pas `--version`.** Sur un outil .NET, `--version` se lit universellement comme
« quelle version de l'outil est-ce que j'exécute », et un commutateur répondant à une autre question
sous le nom que tout le monde connaît déjà serait un piège tendu au premier utilisateur.

**Une solution exige une déclaration.** `--solution` lit les projets qui portent
`<ProducesDiagnosticRules>true</ProducesDiagnosticRules>`, et refuse une solution où aucun ne le fait
plutôt que d'émettre un catalogue vide. Pourquoi la déclaration plutôt que la découverte — chiffres
mesurés à l'appui — est dans [le tour d'horizon](dcat.fr.md#--solution-et-pourquoi-il-exige-une-déclaration).

**Un projet multi-cible est lu via `netstandard2.0`** quand il en produit une, parce que c'est la
build que le compilateur d'un consommateur charge réellement.

## Nommer une destination

| Option | Ce qu'elle règle |
| --- | --- |
| `--namespace <NAMESPACE>` | L'espace de noms que le catalogue généré déclare. |
| `--container <NAME>` | Le nom de la classe statique qui porte les règles. |
| `--output <PATH>` | Où écrire la source C# générée. |

Nommez le conteneur au **singulier** : le site d'utilisation se lit `SonarRule.S1144` — une règle,
nommée. Vos utilisateurs paient ce nom deux fois par suppression et ne peuvent pas le raccourcir.

## Enregistrer la provenance

| Option | Ce qu'elle enregistre |
| --- | --- |
| `--source-name <NAME>` | Ce qu'il faut enregistrer comme source. Par défaut l'id du paquet, le nom d'assemblage du projet, ou celui du premier assemblage. |
| `--source-version <VERSION>` | Ce qu'il faut enregistrer comme release de la source. Par défaut la version du paquet, celle du projet, ou celle de l'assemblage. |
| `--date <yyyy-MM-dd>` | `generate` seulement. La date de génération à estamper. Fixez-la pour rendre une régénération des mêmes entrées identique octet pour octet. |

**Passez `--source-name` et `--source-version` en lisant `--assembly`.** Un catalogue enregistre la
release dont il a été généré, et cet enregistrement est ce qui distingue un instantané du suivant. Un
assemblage construit depuis une copie de travail porte ce que son projet a réglé en dernier — souvent
inchangé d'une reconstruction à l'autre — si bien qu'un catalogue qui en dérive seul peut prétendre à
une source immobile pendant que ses règles bougent dessous.

## Rapport

| Option | Ce qu'elle fait |
| --- | --- |
| `--summary <PATH>` | Écrire un rapport Markdown de ce qui a changé — règles ajoutées, recatégorisées, retitrées, retirées. |

`--summary` est ce qui fait qu'une régénération planifiée ouvre une pull request qu'un humain peut
relire plutôt que fusionner à l'aveugle. [Tenir un catalogue à jour](ci-integration.fr.md) est le
motif qu'elle sert.

## Codes de sortie

| Code | Signification |
| --- | --- |
| `0` | Les catalogues ont été générés, ou `validate` les a trouvés à jour. |
| `1` | L'exécution n'a pas pu aboutir : un paquet amont qui ne résout pas, un analyseur impossible à construire, une sortie impossible à écrire. |
| `2` | `validate` seulement : le catalogue ne correspond plus à sa source. |
| `64` | La ligne de commande est fausse. Aucune reprise n'y changera rien. |

`1` et `2` sont distincts **exprès**. Une panne de flux et un contrat qui a dérivé appellent des
réponses différentes, et un pipeline incapable de les distinguer relancerait une vraie dérive ou
ouvrirait une pull request pour un incident réseau.

`64` est le `EX_USAGE` conventionnel. Branchez dessus quand un job doit échouer bruyamment plutôt que
réessayer.

## D'où viennent les paquets

`dcat` résout via le client NuGet lui-même : il lit donc la hiérarchie `NuGet.config` exactement comme
`dotnet restore` — machine, utilisateur, et chaque dossier en remontant depuis là où vous l'exécutez —
et honore les identifiants qui y sont configurés, **y compris les chiffrés et ceux fournis par un
provider**, qui ne peuvent pas être lus à la main
([ADR-0019](../adr/0019-resolve-packages-through-the-users-own-nuget-configuration.fr.md)).

Un paquet sur un flux privé fonctionne donc sans drapeau supplémentaire. `--source` fixe un flux quand
plusieurs sont configurés.

## Comment les descripteurs sont lus

Les descripteurs sont lus dans un **processus worker séparé**, et trois propriétés en découlent.

**Il progresse jusqu'au dernier majeur installé**, plutôt que le premier trouvé. C'est ce qui empêche
le plancher qui rend `dcat` installable de décider de ce qu'il peut lire : un analyseur construit pour
une cible plus récente se charge quand même, à condition que ce runtime soit présent.

**Le graphe de dépendances de votre analyseur est utilisé quand il en a un.** Si l'analyseur a été
construit avec le SDK, il a un `.deps.json` à côté de lui ; `dcat` le lit et exécute le worker contre
**votre** graphe, si bien qu'un analyseur compilé contre un Roslyn différent de celui que l'outil
transporte est lu via le sien.

Il retombe sur le Roslyn de l'outil quand il n'y a pas de graphe — ce qui est le cas des analyseurs
dépaquetés d'un paquet NuGet, qui voyagent sans — quand votre cache de paquets ne contient pas ce que
le graphe demande, et **quand le graphe ne nomme aucun Roslyn**. Ce dernier point compte parce que
fournir un graphe *remplace* celui du worker au lieu de l'étendre : un graphe sans Roslyn ne laisse pas
le worker avec le sien, il le laisse sans aucun. Le `.deps.json` d'une bibliothèque `netstandard2.0`
est exactement cela, et `dcat` le dit plutôt que de le lire :

```text
resolved MyLib => 1.0.0 (from 1 assembly/assemblies on disk)
  MyLib.deps.json names no Roslyn — reading through this tool's
```

**Un plantage est attribuable.** Un analyseur dont la construction lève emporte le worker et laisse
`dcat` vous dire lequel — plutôt que de faire disparaître toute l'exécution.

## Délais

Les deux processus que `dcat` lance — le worker de descripteurs, et MSBuild pour `--project` et
`--solution` — reçoivent un budget et sont arrêtés s'ils le dépassent.

| Étape | Défaut |
| --- | --- |
| Une lecture de descripteurs | 10 minutes |
| Une évaluation de projet | 2 minutes |

Contre des temps mesurés en secondes. Construire un analyseur, c'est du code tiers, et c'est la seule
étape ici qui peut *se figer* plutôt qu'échouer ; un enfant coincé emporterait sinon l'outil avec lui,
laissant un pipeline tourner jusqu'à son propre délai sans rien à lire.

Réglez `DCAT_TIMEOUT_SECONDS` sur un entier positif de secondes pour allonger les deux.

## Reproductibilité

La même release amont produit les mêmes octets :

* règles et catégories sont émises en ordre **ordinal** ;
* les titres sont lus en **culture invariante** ;
* une règle retirée par l'éditeur est reportée en `[Obsolete]` plutôt que supprimée, parce que les
  consommateurs incorporent les valeurs `const` et qu'en retirer une casse leur recompilation
  ([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.fr.md)).

Fixez `--date` quand deux exécutions des mêmes entrées doivent être identiques octet pour octet. Non
renseignée, elle estampille aujourd'hui, ce qui n'atteint le fichier que si autre chose a changé aussi
— une nuit où l'amont n'a pas bougé ne produit donc aucun diff.

## Runtime

`dcat` cible **.NET 8** et progresse à travers les majeurs : une seule build tourne donc sur .NET 8 et
tout ce qui suit.

## Où aller ensuite

* [**Le manifeste de catalogues**](catalogs-manifest.fr.md) — chaque clé de `catalogs.json`.
* [**Tenir un catalogue à jour**](ci-integration.fr.md) — ces codes de sortie dans un pipeline.
* [**Versionner un catalogue**](versioning-a-catalogue.fr.md) — quoi faire de ce que `--summary`
  rapporte.

---

<div align="center">
<a href="./dcat.fr.md">← L'outil dcat</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./catalogs-manifest.fr.md">Le manifeste de catalogues →</a>
</div>
