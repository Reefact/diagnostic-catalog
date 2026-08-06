# Carte de la documentation

🌍 **Langues :**  
🇬🇧 [English](./README.en.md) | 🇫🇷 Français (ce fichier)

Pour qui doit décider quoi lire. Les pages sont groupées en **pistes**, chacune un ordre de lecture
court qui lui est propre, pour une raison d'être ici différente. Les liens précédent/suivant d'une
page restent à l'intérieur de sa piste : suivre l'une jusqu'au bout vous ramène donc à cette carte,
et non au milieu du chapitre de quelqu'un d'autre.

**La plupart des lecteurs veulent la première piste et rien d'autre.** Les suivantes sont pour qui
publie un catalogue, en génère un, ou travaille sur ce dépôt.

## Utiliser un catalogue

La piste par défaut, et la seule dont vous ayez besoin pour référencer un catalogue et écrire des
suppressions que le compilateur vérifie. Dix minutes de bout en bout, c'est l'étape 2.

<!-- track: using -->

1. [Pourquoi les chaînes magiques échouent](the-problem.fr.md) — les deux arguments d'une
   suppression, leurs deux façons différentes d'échouer, et pourquoi rien dans la plateforme ne peut
   signaler la pire.
2. [Démarrer](getting-started.fr.md) — référencer un catalogue, réécrire une suppression, la casser
   exprès et regarder le compilateur l'attraper.
3. [Concepts](concepts.fr.md) — règle, catalogue, conteneur, classe de catégories, provenance :
   comment cela s'emboîte, quel paquet porte quoi, et ce qu'une référence vous donne exactement.
4. [Écrire des suppressions que le compilateur vérifie](writing-suppressions.fr.md) — la version
   complète : les alias, les littéraux que vous avez déjà, et ce que ceci ne peut pas atteindre.
5. [Configuration](configuration.fr.md) — chaque clé de gravité, l'interrupteur par catégorie, le
   code généré, et l'erreur de `PrivateAssets` qui fait tout taire.
6. [La garantie d'empreinte nulle](zero-footprint.fr.md) — ce qui atteint l'assemblage que vous
   livrez, et ce que le test vérifie réellement.
7. [Quand ne pas s'en servir](when-not-to-use.fr.md) — écrit pour vous en dissuader là où il le
   faut.
8. [Les alternatives](alternatives.fr.md) — un fichier de constantes que vous maintenez,
   `GlobalSuppressions`, `#pragma`, un grep avant chaque montée de version, ne rien faire.

## Adopter les analyseurs sur une base de code qui a déjà des suppressions

Pour une migration plutôt qu'une première suppression : des centaines de littéraux, et une façon de
les convertir qui n'implique pas une semaine de builds rouges.

<!-- track: adopting -->

1. [Adopter un catalogue sur une base de code existante](adopting-a-catalogue.fr.md) — la montée en
   gravité, *Corriger toutes les occurrences* sur un document, un projet ou la solution, le
   cantonnement par dossier, et dans quel ordre convertir.
2. [Les diagnostics `DCAT`](diagnostics.fr.md) — chaque identifiant que vous rencontrerez en
   chemin, ce qui le déclenche, pourquoi il existe, et les clés `.editorconfig` qui le configurent.

## Publier un catalogue

Pour un auteur d'analyseur, ou quiconque possède des règles que personne d'autre ne publie.

<!-- track: publishing -->

1. [Publier un catalogue](authoring-a-catalogue.fr.md) — le contrat structurel, la forme à livrer
   réellement, et déclarer les catégories une seule fois.
2. [Boucler la boucle avec votre propre analyseur](first-party-analyzers.fr.md) — alimenter votre
   `DiagnosticDescriptor` depuis votre propre catalogue, et le membre qui imposerait Roslyn à chaque
   consommateur.
3. [Versionner un catalogue](versioning-a-catalogue.fr.md) — ne jamais supprimer une règle, ne
   jamais renommer un membre, et ce que chaque changement fait au numéro de version.
4. [Empaqueter un catalogue](packaging-a-catalogue.fr.md) — quoi référencer, ce qui se propage, et
   ce que nuget.org fait de votre README.

## Générer et maintenir un catalogue avec `dcat`

Pour qui préfère lire les descripteurs d'un analyseur plutôt que les transcrire.

<!-- track: generating -->

1. [L'outil `dcat`](dcat.fr.md) — les quatre verbes, quelle source lui désigner, et pourquoi il lit
   les descripteurs plutôt que la documentation. Deux diagrammes.
2. [La référence `dcat`](dcat-reference.fr.md) — chaque commande, option et code de sortie, vérifié
   contre les types de réglages de l'outil lui-même.
3. [Le manifeste de catalogues](catalogs-manifest.fr.md) — chaque clé de `catalogs.json`.
4. [Tenir un catalogue à jour](ci-integration.fr.md) — `validate` dans un pipeline, la pull request
   de dérive nocturne, et pourquoi `1` et `2` doivent être traités différemment. Un diagramme.

## Référence et dépannage

Pour une réponse exacte, ou un symptôme.

<!-- track: reference -->

1. [Le contrat de règle](rule-contract.fr.md) — les cinq exigences, comment le marqueur est
   reconnu, et chaque forme syntaxique qu'un site d'utilisation peut prendre.
2. [Dépannage](troubleshooting.fr.md) — les symptômes d'abord : rien n'est signalé, `CS0117`,
   `CS0618`, `DCAT0006` sur tous les fichiers d'un coup. Un diagramme.
3. [FAQ](faq.fr.md) — les questions qui ne sont pas des symptômes.
4. [Glossaire](glossary.fr.md) — chaque mot que cette documentation emploie dans un sens précis, y
   compris ce que chacun n'est *pas*.

## Contribuer à ce dépôt

La piste des internes. Rien de tout cela n'est nécessaire pour *utiliser* quoi que ce soit d'ici.

<!-- track: contributing -->

1. [Architecture du dépôt](architecture.fr.md) — les projets, les découpages imposés chacun par
   quelque chose, la boucle d'auto-application, et où vit chaque sorte de vérification. Un
   diagramme.
2. [Dans le générateur](generator-internals.fr.md) — le chemin que prend une exécution de `dcat`,
   et ce que chaque étape refuse de faire. Un diagramme.
3. [Les trains de release](release-trains.fr.md) — les quinze lignes, comment un projet en rejoint
   un, et la règle inter-trains qui en découle. Un diagramme.
4. [La stratégie de test](testing-strategy.fr.md) — ce que vérifie chaque projet de test, lesquels
   tournent sur le CLR .NET Framework, et la suite que `dotnet test` ne peut pas atteindre.

Plus les deux documents qui ne sont pas des guides :

* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — compiler et tester, le plancher .NET Framework, les
  trains de release, la convention de commit, et comment ajouter un catalogue.
* [**doc/CONVENTIONS.fr.md**](../CONVENTIONS.fr.md) — comment ces documents sont mis en page et ce
  que les tests de documentation vérifient à leur sujet. À lire avant d'ajouter une page.

## Je veux le raisonnement, pas les instructions

Les guides disent quoi faire et pourquoi en une phrase. Là où une décision a demandé un argument,
il est consigné une fois et lié plutôt que répété :

* [**La spécification**](../specification.fr.md) — le document de conception canonique : le contrat
  de règle, le comportement de plateforme sur lequel il repose, le générateur, les diagnostics de
  l'analyseur, l'empaquetage. Normatif, et plus long que n'importe quel guide.
* [**Les décisions d'architecture**](../adr/) — les décisions durables et pourquoi elles ont été
  prises. Commencez par
  [ADR-0008](../adr/0008-express-a-rule-as-a-marked-static-class-of-constants.fr.md) (pourquoi une
  règle est une classe statique marquée de constantes) et
  [ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.fr.md) (pourquoi le
  contenu d'un catalogue est lu depuis les descripteurs et jamais depuis la documentation).

## Je veux le voir marcher plutôt que le lire

L'exemple travaillé est [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) : les
règles `DCAT` de cette bibliothèque, cataloguées par son propre générateur, publiées sur le même
train que les analyseurs qu'elles reflètent. Ce n'est pas une maquette — c'est le produit appliqué à
lui-même, et la CI échoue s'il cesse un jour de décrire les analyseurs livrés à côté de lui.

Les catalogues d'éditeurs sous `src/` sont la même mécanique à l'échelle — de 3 à 456 règles —
reflétant les analyseurs d'autres gens. Ils sont listés dans le
[README du projet](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/README.fr.md#-les-catalogues-disponibles).

---

<div align="center">
<a href="../../README.md">← README du projet</a> · <a href="./the-problem.fr.md">Commencer par Pourquoi les chaînes magiques échouent →</a>
</div>
