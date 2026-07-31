# Conventions de documentation

🌍 **Langues :**  
🇬🇧 [English](./CONVENTIONS.en.md) | 🇫🇷 Français (ce fichier)

<!-- dcat-doc:missing SonarRule.S1144Id cité plus bas comme la forme de nommage écartée par la conception -->
<!-- dcat-doc:missing SonarRule.S1145 cité plus bas comme exemple de contre-exemple volontaire -->

Pour quiconque ajoute ou modifie une page — y compris un agent, ce qui explique pourquoi chaque
règle ci-dessous dit comment elle est vérifiée plutôt que de demander qu'on s'en souvienne. Comment
les documents sous [`doc/`](.) sont disposés, et ce qu'un test vérifie à leur sujet.

Les règles sont appliquées par `tests/DiagnosticCatalog.Documentation.UnitTests`. Une page qui en
casse une casse le build, exactement comme une déclaration de règle qui casse le contrat structurel.
C'est délibéré : ce dépôt existe parce qu'une erreur que rien ne signale est pire qu'une erreur qui
casse bruyamment, et un jeu de documentation est précisément le genre d'artefact où rien ne signale
quoi que ce soit.

## Ce qui vit où

| Chemin | Ce qu'il contient | Langue |
| --- | --- | --- |
| [`doc/guide/`](guide/) | Le jeu de documentation destiné au lecteur. Un dossier plat. | Anglais **et** français |
| [`doc/specification.en.md`](specification.en.md) | Le document de conception normatif. Canonique. | Anglais et français |
| [`doc/adr/`](adr/) | Les décisions d'architecture. | Anglais aujourd'hui ; français à venir |
| `README.md` à la racine du dépôt | La vitrine. | Anglais aujourd'hui ; une version française est à venir |
| `src/*/README.md` | Les pages de paquet sur nuget.org. | Anglais uniquement — voir plus bas |

**Ce que la vérification de parité voit réellement**, c'est tout document dont le nom porte un
suffixe de langue — `<nom>.en.md` ou `<nom>.fr.md`. Les décisions d'architecture sous `doc/adr/`
n'en portent pas encore : elles sont toujours en `NNNN-titre-court.md`, et leur conversion est une
action de suivi consignée dans
[ADR-0022](adr/0022-maintain-every-document-under-doc-in-english-and-french.md). Rien n'est exempté
par une liste, et c'est délibéré — une vérification à liste d'exceptions dérive vers une
vérification qui n'est plus que des exceptions.

**Le dossier du guide est plat exprès.** Chaque lien inter-langues est alors un simple voisin
(`./concepts.fr.md`), et chaque lien de navigation aussi. Une arborescence achèterait un regroupement
que la [carte de la documentation](guide/README.fr.md) fournit déjà en prose, au prix d'un `../`
dans chaque lien — ce qui est la seule chose, dans un jeu Markdown, qui casse silencieusement quand
un fichier bouge.

**Les READMEs de paquet ne font pas partie de ce jeu.** Ils sont livrés dans le `.nupkg` en tant que
`<PackageReadmeFile>` et rendus par nuget.org, qui ne résout aucun lien relatif et n'offre aucun
sélecteur de langue. Ils restent en anglais, en un seul fichier, et pointent vers l'extérieur avec
des adresses absolues `https://github.com/Reefact/diagnostic-catalog/blob/main/...`. Deux tests les
lisent déjà — `DocumentedMirrorTests` et `DocumentedSiblingsTests` — si bien que leur contenu est
contraint par plus que ce fichier.

## Nommage

Une page est `<nom-en-kebab-case>.<lang>.md`, où `<lang>` vaut `en` ou `fr` :

```
doc/guide/getting-started.en.md
doc/guide/getting-started.fr.md
```

Kebab-case, parce que c'est ce que le reste du dépôt utilise déjà — `specification.en.md`,
`0001-floor-the-libraries-on-net-framework-4-7-2.md` — et qu'un ensemble de fichiers qui se nomme de
deux façons n'apprend rien au lecteur, sinon que personne n'a tranché.

**Le nom est en anglais dans les deux langues.** `getting-started.fr.md`, jamais `demarrage.fr.md`.
Un nom de fichier est une adresse : il apparaît dans les liens des pages anglaises, dans les tickets,
dans les commentaires de revue, et dans ce fichier. Le traduire doublerait les adresses d'un même
document et ferait de chaque lien inter-langues une recherche.

## Chaque page porte les trois mêmes choses

Les deux premières lient tout document sous `doc/` — le guide, la spécification, les décisions
d'architecture. La troisième ne lie que [`doc/guide/`](guide/) : c'est le dossier qui a un ordre de
lecture, et le pied de navigation est ce qui l'exprime.

### 1. Un H1, puis le bandeau de langue

Le bandeau est le deuxième bloc du fichier, immédiatement après le titre, et c'est le seul endroit
où l'autre langue est proposée :

```markdown
# Getting started

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./getting-started.fr.md)
```

```markdown
# Démarrage

🌍 **Langues :**  
🇬🇧 [English](./getting-started.en.md) | 🇫🇷 Français (ce fichier)
```

Les deux espaces en fin de ligne après `**Langues :**` constituent un saut de ligne dur et sont
porteurs ; sans eux la ligne des drapeaux rejoint la ligne du libellé. C'est pour cette raison
qu'`.editorconfig` refuse déjà de rogner les espaces de fin en Markdown.

*Vérifié :* le bandeau suit le H1, nomme les deux langues, et son lien résout vers le fichier voisin.

### 2. Une phrase qui dit à qui la page s'adresse

Directement sous le bandeau, avant tout titre. Pas un résumé de la page — un filtre, pour que le
lecteur qui n'en est pas le public s'arrête là :

```markdown
For anyone who writes `[SuppressMessage(...)]`. You do not need to know anything about how
DiagnosticCatalog works to read this.
```

*Vérifié :* rien. Celle-ci repose sur la revue, parce qu'aucun test ne sait distinguer un filtre d'un
résumé.

### 3. Le pied de navigation

Le dernier bloc du fichier :

```markdown
---

<div align="center">
<a href="./the-problem.en.md">← Why magic strings fail</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./concepts.en.md">Core concepts →</a>
</div>
```

* Le lien du milieu est toujours présent et pointe toujours vers la carte — `README.en.md` ou
  `README.fr.md` dans le même dossier. La carte elle-même fait exception : elle *est* la table des
  matières, donc son pied ne porte qu'un lien de retour vers le README du projet et un lien vers la
  première page.
* `←` est absent sur la première page de l'ordre de lecture ; `→` est absent sur la dernière. La
  carte n'est pas une étape de cet ordre — elle en est l'entrée — si bien que le `←` de la première
  page est réellement absent plutôt que de renvoyer vers une table des matières que son `↑` propose
  déjà.
* Le texte du lien est le titre de la page visée, pour que le lecteur sache ce qu'il s'apprête à
  ouvrir.
* `<div align="center">` plutôt qu'une construction Markdown : GitHub retire la plupart des styles
  en ligne du Markdown mais honore celui-ci, et c'est ce qu'utilise le projet frère
  [`first-class-errors`](https://github.com/Reefact/first-class-errors). S'y aligner fait qu'un
  lecteur passant d'un dépôt à l'autre ne rencontre qu'une seule convention.

*Vérifié, et c'est la vérification stricte :* les pieds de toutes les pages anglaises doivent décrire
**un ordre total** — chaque page atteignable, exactement une page sans prédécesseur, exactement une
sans successeur, aucun cycle, et chaque `←` l'inverse exact du `→` correspondant. Les pages
françaises doivent décrire le même ordre. Une page ajoutée sans être enfilée dans la chaîne échoue,
et c'est ce qui empêche le jeu de faire pousser une orpheline que personne ne lie.

## L'ordre de lecture est celui de la carte

[`guide/README.fr.md`](guide/README.fr.md) est la carte de la documentation : elle groupe les pages
par ce que le lecteur cherche à faire, et son ordre est celui que les pieds enfilent. Ajouter une
page veut dire l'ajouter à la carte **et** à la chaîne ; le test compare les deux et échoue si elles
divergent.

## Règles de rédaction

* **L'anglais fait foi.** Là où les deux versions divergent, c'est l'anglaise qui gagne — la règle
  que la spécification énonce déjà. Une page française est une traduction, jamais un document
  indépendant, et une modification faite en français seul est une modification qui sera perdue.
* **Coupez à 100 colonnes.** C'est ce que font les guides existants. La prose se recompose ; un
  fichier à 100 colonnes produit un diff lisible, et un fichier à un paragraphe par ligne produit un
  diff que personne ne lit.
* **Ne traduisez jamais un identifiant.** Identifiants de règles, noms de paquets, noms de membres,
  propriétés MSBuild, clés `.editorconfig`, noms de commandes, codes de sortie et chemins de fichiers
  sont les mêmes dans les deux langues, parce qu'ils sont les mêmes dans le code. Une page française
  explique `DCAT0006` ; elle ne le renomme pas.
* **Les exemples de code sont partagés, pas traduits.** Le C# d'une page française EST le C# de la
  page anglaise, caractère pour caractère, noms d'identifiants compris. Seuls les commentaires à
  l'intérieur d'un exemple sont traduits — et seulement quand le commentaire est de la prose. Un
  exemple qui diffère entre les langues est un exemple que l'une des deux a raté.
* **Les exemples C# suivent les règles de codage du dépôt.** Écrivez le type, jamais `var`
  ([`CLAUDE.md`](../CLAUDE.md)). Un lecteur copie ce qu'il voit, et une documentation qui enseigne un
  style que le build rejette est pire qu'une documentation qui n'enseigne rien.
* **Les libellés d'un schéma sont de la prose.** La règle ci-dessus lie les exemples C#, XML et
  `.editorconfig`, dont les identifiants sont ceux du code. Les libellés de nœuds d'un schéma mermaid
  sont des phrases, et ils se traduisent comme n'importe quelle autre phrase de la page.
* **Préférez une affirmation vérifiable.** « Mesuré contre une vraie restauration » vaut mieux que
  « devrait fonctionner ». Là où un comportement est asserté par un test, nommez le test.

## Schémas

**Mermaid par défaut**, dans un bloc ```` ```mermaid ````. GitHub le rend nativement, dans le thème
clair ou sombre du lecteur, et — la raison qui compte ici — c'est du texte : un schéma change dans un
diff relisible au lieu d'arriver sous forme d'un binaire que personne ne peut comparer.

Ne prenez un SVG sous `doc/images/` que quand la figure n'est pas un graphe : un
avant/après, une illustration annotée, tout ce dont la disposition porte le sens. Alors :

* le fichier est livré en SVG, jamais en PNG, pour rester lisible à tout zoom et se diffuser comme du
  texte ;
* il fonctionne sur les **deux** thèmes GitHub. Soit la figure est neutre, soit la page propose deux
  fichiers via un élément `<picture>` :

  ```html
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../images/two-failures.dark.svg">
    <img alt="A wrong id lets the warning return; a wrong category does nothing, ever."
         src="../images/two-failures.light.svg">
  </picture>
  ```
* `alt` dit ce que la figure *affirme*, pas ce qu'elle représente. Un lecteur au lecteur d'écran
  reçoit le propos de la figure, ou rien du tout.

La même figure sert les deux langues quand elle ne porte aucun mot. Quand elle en porte, elle prend
une paire `.en.svg` / `.fr.svg` — et ce coût est la raison de garder les mots hors des figures.

*Vérifié :* chaque image référencée par une page existe, et chaque image sous `doc/images/` est
référencée par au moins une page.

## Liens

* Relatifs, toujours, entre documents de ce dépôt. Les adresses absolues
  `https://github.com/Reefact/diagnostic-catalog/...` uniquement dans les READMEs de paquet, où les
  liens relatifs ne résolvent pas.
* Une ancre (`#titre-de-section`) doit exister dans le document visé.

*Vérifié :* chaque lien relatif de `doc/` et du README racine résout vers un fichier qui existe, et
chaque ancre résout vers un titre du document visé.

## Ce contre quoi la documentation est vérifiée

Deux assertions sortent de la documentation pour aller dans le code, et elles sont tout l'intérêt du
projet de test :

* **Chaque diagnostic `DCAT` que les analyseurs livrent est documenté, et chaque `DCAT` que le guide
  documente est livré.** Un nouveau diagnostic ne peut pas atteindre une release sans page qui le
  décrive, et une page ne peut pas décrire un diagnostic qui n'a jamais été implémenté.
* **Chaque option `dcat` que la documentation mentionne existe sur les types de configuration de
  l'outil.** Un drapeau documenté après avoir été renommé ou retiré échoue. La réciproque — chaque
  option exposée par l'outil est documentée — attend la page de référence `dcat` ; tant qu'elle
  n'existe pas, aucun document unique ne peut porter l'obligation, et l'étaler sur chaque fichier qui
  mentionne l'outil rendrait la vérification insatisfiable plutôt que stricte.
* **Chaque règle qu'un exemple montre est une règle publiée par son catalogue.** `SonarRule.S1144`
  résout contre le `DiagnosticCatalog.Sonar` compilé, et le conteneur n'est jamais mis au pluriel.
  Celle-ci existe parce qu'elle avait déjà manqué : seize exemples répartis sur trois documents
  écrivaient le conteneur `SonarRules`, et aucun d'eux ne compilait.

Les trois comparent un document à la vérité compilée plutôt qu'à un autre document. C'est le même
raisonnement qu'[ADR-0009](adr/0009-generate-catalog-content-from-analyzer-descriptors.md) : les
descripteurs sont ce avec quoi l'analyseur signale, donc ce contre quoi une affirmation à leur sujet
se vérifie.

### Montrer une référence qui n'existe pas

Certaines pages y sont obligées. Le tutoriel demande au lecteur de casser une référence et de lire le
`CS0117` qu'elle produit — une règle qui existerait gâcherait l'étape — et la page des concepts
montre `SonarRule.S1144Id` comme la forme de nommage que la conception a écartée. Déclarez-la dans le
document, avec la raison :

```markdown
<!-- dcat-doc:missing SonarRule.S1145 l'erreur volontaire de l'étape 3 -->
```

Déclarée dans le document et non dans le test, pour qu'un lecteur des sources rencontre la raison là
où est l'exemption. Par document, pour que la même faute sur n'importe quelle autre page échoue
encore. La raison est obligatoire. Et une déclaration qui nomme une référence que la page ne montre
plus échoue aussi — une exemption que rien n'utilise couvre ce qui sera écrit là ensuite.

## Ajouter une page

1. Écrivez `doc/guide/<nom>.en.md` avec le bandeau, la phrase de public et le pied.
2. Écrivez `doc/guide/<nom>.fr.md` dans le même commit. Une page fusionnée avec « le français
   suivra » n'obtient pas son français, et le test de parité refuse de la laisser essayer.
3. Insérez-la dans l'ordre de lecture : ajoutez une ligne à la carte, et ajustez les `←`/`→` de ses
   deux voisines dans les deux langues.
4. Lancez `dotnet test -c Release` et lisez ce que disent les tests de documentation.
