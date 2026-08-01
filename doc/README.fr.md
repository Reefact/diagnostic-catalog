# Documentation

🌍 **Langues :**  
🇬🇧 [English](./README.en.md) | 🇫🇷 Français (ce fichier)

Quatre sortes de documents vivent ici, et elles répondent à des questions différentes. Cette
page dit laquelle vous cherchez.

| Si vous voulez… | Lisez | Forme |
| --- | --- | --- |
| *faire* quelque chose | [**Le guide**](guide/README.fr.md) | 26 pages, enfilées dans un ordre unique, chacune avec précédent/suivant |
| le comportement exact, normativement | [**La spécification**](specification.fr.md) | Un long document de conception |
| savoir *pourquoi* c'est ainsi | [**Les décisions d'architecture**](adr/) | Un fichier par décision, daté, jamais modifié une fois accepté |
| ajouter une page ici | [**Les conventions**](CONVENTIONS.fr.md) | La disposition, et ce que les tests vérifient |

## Commencez par le guide

[**La carte de la documentation**](guide/README.fr.md) choisit une page selon ce que vous
cherchez à faire, et chaque page porte la précédente et la suivante — elle se lit donc aussi
d'un bout à l'autre.

Six pistes, dans l'ordre de lecture :

* **Découverte** — est-ce pour moi ? Le problème, les concepts, quand *ne pas* s'en servir,
  et les alternatives.
* **Se servir d'un catalogue** — le cas courant : écrire des suppressions que le compilateur
  vérifie, en adopter un sur une base existante, la configuration, et ce qui atteint
  l'assemblage que vous livrez (rien).
* **Publier un catalogue** — le contrat structurel, boucler la boucle avec votre propre
  analyseur, le versionnage, l'empaquetage.
* **En générer un** — l'outil `dcat`, sa référence complète, le manifeste, et tenir un
  catalogue à jour en CI.
* **Référence** — chaque diagnostic `DCAT`, le contrat de règle, le dépannage par symptôme,
  la FAQ, le glossaire.
* **Internes** — pour les contributeurs seulement : l'architecture du dépôt, le générateur,
  les trains de release, la stratégie de test.

## La spécification

[La spécification](specification.fr.md) est le document de conception canonique : le contrat
de règle, le comportement de la plateforme sur lequel il repose, le générateur, les
diagnostics des analyseurs, l'empaquetage. Elle est normative et plus longue que n'importe
quel guide — lisez-la quand il vous faut la réponse exacte plutôt que la réponse utilisable.

Son annexe mérite d'être connue à part : chaque affirmation de comportement sur laquelle la
conception repose a été vérifiée contre la plateforme plutôt que supposée, et l'annexe
enregistre ce qui a été vérifié et comment.

## Les décisions d'architecture

[Les ADR](adr/) enregistrent les décisions qu'un futur mainteneur remettrait en question,
avec le contexte, les alternatives rejetées et pourquoi, et les conséquences acceptées.
C'est un journal historique : un enregistrement accepté n'est jamais modifié, et une
décision se révise en écrivant un successeur qui la remplace.

Deux sont un bon point de départ, parce que la plupart des autres en découlent :

* [ADR-0008](adr/0008-express-a-rule-as-a-marked-static-class-of-constants.md) — pourquoi
  une règle est une classe statique de constantes marquée, plutôt qu'une interface ou une
  classe de base.
* [ADR-0009](adr/0009-generate-catalog-content-from-analyzer-descriptors.md) — pourquoi le
  contenu d'un catalogue est lu dans les descripteurs des analyseurs eux-mêmes et jamais
  dans leur documentation.

## Les conventions

[CONVENTIONS.fr.md](CONVENTIONS.fr.md) est le contrat que ces documents suivent : la
disposition des fichiers, le bandeau de langue, le pied de navigation, les règles d'écriture
et de diagrammes — et, à côté de chaque règle, comment elle est vérifiée. À lire avant
d'ajouter une page.

## Les deux langues

Chaque document de ce dossier existe en page anglaise et en page française, et le bandeau en
haut de chacune passe de l'une à l'autre. **L'anglais fait foi** : là où les deux divergent,
c'est la version anglaise qui a raison
([ADR-0022](adr/0022-maintain-every-document-under-doc-in-english-and-french.md)).

Une page et sa traduction arrivent dans le même commit, et
`tests/DiagnosticCatalog.Documentation.UnitTests` fait échouer une paire à laquelle il
manque une moitié, un lien qui ne résout pas, ou une page vers laquelle rien ne navigue.

Deux choses restent délibérément hors de cette règle. Les [décisions
d'architecture](adr/) sont en anglais seulement, comme tout ce que ce dépôt enregistre comme
histoire. Les README de paquets sous [`src/`](../src) le sont aussi, parce que nuget.org
affiche un seul fichier par paquet, n'offre aucun sélecteur de langue et ne résout aucun
lien relatif.

---

<div align="center">
<a href="../README.md">← README du projet (en anglais)</a> · <a href="./guide/README.fr.md">La carte de la documentation →</a>
</div>
