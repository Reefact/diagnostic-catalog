# ADR-0027 | Livrer les diagnostics côté usage en erreurs

🌍 **Langues :**  
🇫🇷 Français (ce fichier) | 🇬🇧 [English](./0027-ship-the-use-site-diagnostics-as-errors.en.md)

**Status:** Superseded by [ADR-0040](0040-grade-every-dcat-diagnostic-by-what-it-says.fr.md)
**Proposed:** 2026-08-02
**Accepted:** 2026-08-02
**Decision Makers:** Reefact

## Context

Tous les diagnostics `DCAT` allaient être livrés en `Warning`. Rien ne l'avait décidé :
c'est le défaut qu'un `DiagnosticDescriptor` reçoit quand l'auteur n'y pense pas, et
aucun ADR ne s'était jamais penché dessus.

Demandons plutôt à quoi sert de référencer `DiagnosticCatalog.Sonar`. Une équipe
l'ajoute pour qu'aucune suppression de son codebase ne soit une chaîne magique — pour
que `[SuppressMessage("Major Code Smell", "S1144")]` devienne
`[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]` et le reste quand le
fournisseur déplace la règle, quand un renommage traverse la solution, quand quelqu'un
cherche qui supprime quoi.

Cette propriété n'est pas partielle. Un codebase dont la moitié des suppressions sont
des références et l'autre des littéraux ne l'a pas ; il l'a là où quelqu'un y a pensé.
La garantie est une propriété de l'ensemble, et un diagnostic qui signale le manque en
avertissement laisse cet ensemble à l'attention de chacun.

Le guide de configuration avait déjà tiré cette conclusion dans sa propre colonne de
conseil, recommandant `error` pour `DCAT0001` et `DCAT0007`, et « error une fois
converti » pour `DCAT0006`. Le défaut livré était simplement en retard sur la
documentation qui le décrivait.

## Decision

**Les trois diagnostics côté usage sont livrés en `DiagnosticSeverity.Error` :**
`DCAT0001`, `DCAT0006`, `DCAT0007`.

Les autres restent en `Warning` :

* `DCAT0002`, `DCAT0003`, `DCAT0004` s'adressent à qui **écrit** un catalogue, pas à qui
  en consomme un. Audience différente, build différent, et pour un catalogue généré le
  générateur les garantit déjà.
* `DCAT0009` est côté usage et y aurait sa place, mais il rate encore un identifiant
  atteint via une constante. Promouvoir une règle qui sous-détecte casse les builds de
  façon inégale, pour une raison que l'auteur ne peut pas voir depuis le diagnostic.

## Rationale

L'argument porte sur le sens dans lequel le défaut doit se tromper.

En `Warning`, une équipe qui veut la garantie doit savoir qu'une ligne existe, la
trouver, et l'écrire. La plupart ne le feront jamais, et celles qui en ont le plus
besoin sont les moins susceptibles de lire le guide de configuration. En `Error`, une
équipe qui n'en veut **pas** écrit une ligne, délibérément, après avoir lu le message
qui lui a dit pourquoi.

Les deux coûtent une ligne. Une seule des deux se découvre toute seule.

La sévérité est surchargeable par règle et par chemin via un `.editorconfig` ordinaire —
pas de format propriétaire, pas de propriété MSBuild — donc `Error` n'est une position
dont personne n'est prisonnier :

```ini
dotnet_diagnostic.DCAT0006.severity = suggestion
```

## Consequences

**Référencer le paquet peut faire passer un build vert au rouge.** Un codebase avec des
suppressions littérales existantes échoue au premier build après l'ajout. C'est le
signal voulu, et aussi le pire moment pour le rencontrer : le guide de configuration
donne donc la ligne de repli juste à côté du tableau, et nomme `DCAT0006` — le seul des
trois qui signale *du travail pas encore fait* plutôt que *quelque chose de déjà faux*.

**Ce n'est pas un changement cassant.** `DiagnosticCatalog.Analyzers` est publié pour la
première fois en `1.0.0-preview.1`. Aucun consommateur n'a de build que ceci modifie ; la
sévérité fait partie de ce qu'est le paquet le jour où il apparaît.

**Le correctif de la constante intermédiaire est devenu un prérequis.** Une suppression
nommant un membre de règle hissé dans une constante nommée était signalée par
`DCAT0007` — un faux positif que la liste des formes acceptées du guide contredisait.
En avertissement, c'était du bruit. En erreur, cela casserait le build de quelqu'un qui
fait exactement ce que la documentation demande, donc `SuppressionAttribute.Resolve`
suit désormais un saut dans l'initialiseur d'une constante. C'est passé en premier, avec
un test vu échouer contre l'analyzer non corrigé.

**La politique est verrouillée par un test.** `DefaultSeverityTests` vérifie la sévérité
par défaut de chaque descripteur livré, et qu'aucun n'est désactivé. Avant son
existence, les trois sévérités ont été changées et toute la suite est restée verte — une
politique que rien n'observe est une politique qui dérive.

## Follow-up Actions

* Promouvoir `DCAT0009` une fois qu'il détecte un identifiant atteint via une constante.
* Reconsidérer `DCAT0002`–`DCAT0004` si les catalogues écrits à la main s'avèrent
  courants ; le raisonnement ici porte sur l'audience, pas sur une sévérité qui leur
  importerait peu.
