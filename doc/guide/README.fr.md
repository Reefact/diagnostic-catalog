# Carte de la documentation

🌍 **Langues :**  
🇬🇧 [English](./README.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque hésite sur ce qu'il faut lire. Cette page est organisée par ce que vous cherchez à
**faire**, pas par la façon dont le code est rangé.

## J'écris `[SuppressMessage(...)]` et je veux que ce soit vérifié

Le cas courant, et celui qui ne demande de connaître rien d'autre ici.

1. [**Écrire des suppressions que le compilateur vérifie**](writing-suppressions.fr.md) —
   référencer un catalogue, écrire la suppression contre des constantes, migrer les littéraux que
   vous avez déjà, et voir ce que cela coûte à l'exécution (rien).

Puis, quand un diagnostic apparaît :

* [Les diagnostics `DCAT`](diagnostics.fr.md) — ce que chacun signifie, et comment configurer sa
  gravité.

## Je livre un analyseur, ou je possède des règles que personne d'autre ne publie

1. [**Publier un catalogue**](authoring-a-catalogue.fr.md) — le contrat structurel, la forme à
   livrer réellement, déclarer les catégories une seule fois, l'empaquetage, et la règle de
   versionnement qui vous mordra si vous la sautez.
2. [Les diagnostics `DCAT`](diagnostics.fr.md) — ce qu'on dira à vos utilisateurs, et quand.

## J'ai vu passer un `DCATxxxx` et je veux savoir ce que c'est

* [**Les diagnostics `DCAT`**](diagnostics.fr.md) — chaque identifiant, ce qui le déclenche,
  pourquoi il existe, et les clés `.editorconfig` qui le configurent.

## Je veux le raisonnement, pas les instructions

Les guides ci-dessus disent quoi faire et donnent le pourquoi en une phrase. Là où une décision a
demandé un argument, il est consigné une fois et lié plutôt que répété :

* [**La spécification**](../specification.fr.md) — le document de conception canonique : le contrat
  de règle, le comportement de la plateforme sur lequel il repose, le générateur, les diagnostics
  des analyseurs, l'empaquetage. Normatif, et plus long que n'importe quel guide. La version
  anglaise fait foi.
* [**Les décisions d'architecture**](../adr/) — les décisions durables et pourquoi elles ont été
  prises. Commencez par
  [ADR-0008](../adr/0008-express-a-rule-as-a-marked-static-class-of-constants.md) (pourquoi une
  règle est une classe statique marquée, faite de constantes) et
  [ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.md) (pourquoi le
  contenu d'un catalogue est lu dans les descripteurs et jamais dans la documentation).

## Je préfère le voir tourner que le lire

L'exemple travaillé est [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) : les
règles `DCAT` de cette bibliothèque, cataloguées par son propre générateur, publiées sur le même
train que les analyseurs qu'elles reflètent. Ce n'est pas une maquette — c'est le produit appliqué à
lui-même, et la CI échoue s'il cesse un jour de décrire les analyseurs livrés à côté de lui.

Les trois catalogues d'éditeurs sous `src/` sont la même mécanique à l'échelle — 465, 318 et 193
règles — reflétant les analyseurs d'autres gens.

## Je contribue à ce dépôt

* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — compiler et tester, le plancher .NET Framework, les
  trains de release, la convention de commit, et comment ajouter un catalogue.
* [**doc/CONVENTIONS.en.md**](../CONVENTIONS.en.md) — comment ces documents sont disposés et ce que les
  tests de documentation vérifient à leur sujet. À lire avant d'ajouter une page.

## Ordre de lecture suggéré

Tout ce dossier est enfilé dans un ordre unique, et le pied de chaque page porte la précédente et la
suivante. Le suivre de bout en bout vous mène d'une simple suppression à la publication de votre
propre catalogue :

1. [Écrire des suppressions que le compilateur vérifie](writing-suppressions.fr.md)
2. [Publier un catalogue](authoring-a-catalogue.fr.md)
3. [Les diagnostics `DCAT`](diagnostics.fr.md)

---

<div align="center">
<a href="../../README.md">← README du projet (en anglais)</a> · <a href="./writing-suppressions.fr.md">Commencer par Écrire des suppressions →</a>
</div>
