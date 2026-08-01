# Carte de la documentation

🌍 **Langues :**  
🇬🇧 [English](./README.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque hésite sur ce qu'il faut lire. Cette page est organisée par ce que vous cherchez à
**faire**, pas par la façon dont le code est rangé.

## Je cherche à savoir si c'est pour moi

* [**Démarrer**](getting-started.fr.md) — dix minutes : référencer un catalogue, réécrire une
  suppression, la casser exprès et regarder le compilateur l'attraper.
* [**Pourquoi les chaînes magiques échouent**](the-problem.fr.md) — les deux arguments d'une
  suppression, les deux façons différentes dont ils échouent, et pourquoi rien dans la plateforme ne
  peut signaler la pire des deux.
* [**Quand ne pas s'en servir**](when-not-to-use.fr.md) — écrite pour vous en dissuader là où il le
  faut.
* [**Les alternatives**](alternatives.fr.md) — un fichier de constantes que vous maintenez,
  `GlobalSuppressions`, `#pragma`, un grep avant chaque montée de version, ne rien faire.

## J'écris `[SuppressMessage(...)]` et je veux que ce soit vérifié

Le cas courant, et celui qui ne demande de connaître rien d'autre ici.

* [**Écrire des suppressions que le compilateur vérifie**](writing-suppressions.fr.md) — référencer
  un catalogue, écrire la suppression contre des constantes, migrer les littéraux que vous avez déjà,
  et voir ce que cela coûte à l'exécution (rien).
* [**Adopter un catalogue sur une base de code existante**](adopting-a-catalogue.fr.md) — la rampe de
  gravité, *Corriger toutes les occurrences*, le cantonnement par dossier, et dans quel ordre
  convertir.
* [**Configuration**](configuration.fr.md) — chaque clé de gravité, le commutateur par catégorie, le
  code généré, et l'erreur de `PrivateAssets` qui fait tout taire.
* [**La garantie d'empreinte nulle**](zero-footprint.fr.md) — ce qui atteint l'assemblage que vous
  livrez, et ce que le test asserte réellement.
* [Concepts](concepts.fr.md) — si un mot de ce guide vous est étranger.
* [Les diagnostics `DCAT`](diagnostics.fr.md) — quand l'un d'eux apparaît.

## Je livre un analyseur, ou je possède des règles que personne d'autre ne publie

* [**Publier un catalogue**](authoring-a-catalogue.fr.md) — le contrat structurel, la forme à livrer
  réellement, déclarer les catégories une seule fois, l'empaquetage, et la règle de versionnement qui
  vous mordra si vous la sautez.
* [**Boucler la boucle avec votre propre analyseur**](first-party-analyzers.fr.md) — alimenter votre
  `DiagnosticDescriptor` depuis votre propre catalogue, et le membre qui imposerait Roslyn à tous vos
  consommateurs.
* [**Versionner un catalogue**](versioning-a-catalogue.fr.md) — ne jamais supprimer une règle, ne
  jamais renommer un membre, et ce que chaque changement fait au numéro de version.
* [**Empaqueter un catalogue**](packaging-a-catalogue.fr.md) — quoi référencer, ce qui se propage, et
  ce que nuget.org fait de votre README.
* [Les diagnostics `DCAT`](diagnostics.fr.md) — ce qu'on dira à vos utilisateurs, et quand.

## Je génère un catalogue plutôt que de l'écrire à la main

* [**L'outil `dcat`**](dcat.fr.md) — les quatre verbes, quelle source lui désigner, et pourquoi il lit
  des descripteurs plutôt que de la documentation. Deux schémas.
* [**La référence `dcat`**](dcat-reference.fr.md) — chaque commande, option et code de sortie, vérifiés
  contre les types de configuration de l'outil.
* [**Le manifeste de catalogues**](catalogs-manifest.fr.md) — chaque clé de `catalogs.json`.
* [**Tenir un catalogue à jour**](ci-integration.fr.md) — `validate` dans un pipeline, la pull request
  de dérive nocturne, et pourquoi `1` et `2` doivent être traités différemment. Un schéma.

## J'ai vu passer un `DCATxxxx` et je veux savoir ce que c'est

* [**Les diagnostics `DCAT`**](diagnostics.fr.md) — chaque identifiant, ce qui le déclenche,
  pourquoi il existe, et les clés `.editorconfig` qui le configurent.

## Il me faut une référence, pas un tutoriel

* [**Le contrat de règle**](rule-contract.fr.md) — les quatre exigences, comment le marqueur est
  apparié, et toutes les formes syntaxiques qu'un site d'utilisation peut prendre.
* [**Dépannage**](troubleshooting.fr.md) — les symptômes d'abord : rien n'est signalé, `CS0117`,
  `CS0618`, `DCAT0006` sur tous les fichiers d'un coup. Un schéma.
* [**FAQ**](faq.fr.md) — les questions qui ne sont pas des symptômes.
* [**Glossaire**](glossary.fr.md) — chaque mot que cette documentation emploie dans un sens précis.

## Je veux le vocabulaire

* [**Concepts**](concepts.fr.md) — règle, catalogue, conteneur, classe de catégories, provenance ;
  comment ils s'imbriquent, quel paquet porte quoi, et ce qu'une référence vous donne exactement
  aujourd'hui.
* [**Glossaire**](glossary.fr.md) — les mêmes mots, définis un par un, y compris ce que chacun n'est
  *pas*.

## Je veux le raisonnement, pas les instructions

Les guides disent quoi faire et donnent le pourquoi en une phrase. Là où une décision a demandé un
argument, il est consigné une fois et lié plutôt que répété :

* [**La spécification**](../specification.fr.md) — le document de conception canonique : le contrat
  de règle, le comportement de la plateforme sur lequel il repose, le générateur, les diagnostics
  des analyseurs, l'empaquetage. Normatif, et plus long que n'importe quel guide. La version
  anglaise fait foi.
* [**Les décisions d'architecture**](../adr/) — les décisions durables et pourquoi elles ont été
  prises. Commencez par
  [ADR-0008](../adr/0008-express-a-rule-as-a-marked-static-class-of-constants.fr.md) (pourquoi une
  règle est une classe statique marquée, faite de constantes) et
  [ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.fr.md) (pourquoi le
  contenu d'un catalogue est lu dans les descripteurs et jamais dans la documentation).

## Je préfère le voir tourner que le lire

L'exemple travaillé est [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) : les
règles `DCAT` de cette bibliothèque, cataloguées par son propre générateur, publiées sur le même
train que les analyseurs qu'elles reflètent. Ce n'est pas une maquette — c'est le produit appliqué à
lui-même, et la CI échoue s'il cesse un jour de décrire les analyseurs livrés à côté de lui.

Les trois catalogues d'éditeurs sous `src/` sont la même mécanique à l'échelle — 465, 318 et 193
règles — reflétant les analyseurs d'autres gens.

## Je contribue à ce dépôt

Les quatre pages ci-dessous sont la piste « internes » : elles expliquent comment le dépôt est
assemblé, et aucune n'est nécessaire pour *se servir* de tout ceci.

* [**Architecture du dépôt**](architecture.fr.md) — les huit projets, les quatre découpages que
  quelque chose impose, la boucle d'auto-application, et où vit chaque type de vérification. Un
  diagramme.
* [**Dans le générateur**](generator-internals.fr.md) — le chemin qu'une exécution `dcat` emprunte, et
  ce que chaque étape refuse de faire. Un diagramme.
* [**Les trains de release**](release-trains.fr.md) — les cinq lignes, comment un projet en rejoint
  une, et la règle inter-trains qui s'ensuit. Un diagramme.
* [**La stratégie de test**](testing-strategy.fr.md) — ce que chacun des sept projets de test asserte,
  lesquels tournent sur le CLR .NET Framework, et la suite que `dotnet test` ne peut pas atteindre.

Plus les deux documents qui ne sont pas des guides :

* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — compiler et tester, le plancher .NET Framework, les
  trains de release, la convention de commit, et comment ajouter un catalogue.
* [**doc/CONVENTIONS.en.md**](../CONVENTIONS.en.md) — comment ces documents sont disposés et ce que
  les tests de documentation vérifient à leur sujet. À lire avant d'ajouter une page.

## Ordre de lecture suggéré

Chaque page de ce dossier est enfilée dans un ordre unique, et le pied de chacune porte la précédente
et la suivante. Le suivre de bout en bout vous mène d'une simple suppression à la publication de
votre propre catalogue, puis dans le dépôt lui-même :

1. [Démarrer](getting-started.fr.md)
2. [Pourquoi les chaînes magiques échouent](the-problem.fr.md)
3. [Concepts](concepts.fr.md)
4. [Quand ne pas s'en servir](when-not-to-use.fr.md)
5. [Les alternatives](alternatives.fr.md)
6. [Écrire des suppressions que le compilateur vérifie](writing-suppressions.fr.md)
7. [Adopter un catalogue sur une base de code existante](adopting-a-catalogue.fr.md)
8. [Configuration](configuration.fr.md)
9. [La garantie d'empreinte nulle](zero-footprint.fr.md)
10. [Publier un catalogue](authoring-a-catalogue.fr.md)
11. [Boucler la boucle avec votre propre analyseur](first-party-analyzers.fr.md)
12. [Versionner un catalogue](versioning-a-catalogue.fr.md)
13. [Empaqueter un catalogue](packaging-a-catalogue.fr.md)
14. [L'outil `dcat`](dcat.fr.md)
15. [La référence `dcat`](dcat-reference.fr.md)
16. [Le manifeste de catalogues](catalogs-manifest.fr.md)
17. [Tenir un catalogue à jour](ci-integration.fr.md)
18. [Les diagnostics `DCAT`](diagnostics.fr.md)
19. [Le contrat de règle](rule-contract.fr.md)
20. [Dépannage](troubleshooting.fr.md)
21. [FAQ](faq.fr.md)
22. [Glossaire](glossary.fr.md)

Puis, pour les contributeurs seulement :

23. [Architecture du dépôt](architecture.fr.md)
24. [Dans le générateur](generator-internals.fr.md)
25. [Les trains de release](release-trains.fr.md)
26. [La stratégie de test](testing-strategy.fr.md)

---

<div align="center">
<a href="../../README.md">← README du projet (en anglais)</a> · <a href="./getting-started.fr.md">Commencer par Démarrer →</a>
</div>
