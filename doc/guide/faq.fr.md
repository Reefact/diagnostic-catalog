# FAQ

🌍 **Langues :**  
🇬🇧 [English](./faq.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque pèse une question plutôt que de traquer un symptôme. Si votre build vous dit quelque
chose, [le dépannage](troubleshooting.fr.md) est l'autre page.

## Est-ce que ça ajoute quelque chose à ce que je livre ?

Non. `SuppressMessageAttribute` est `[Conditional("CODE_ANALYSIS")]` : à moins que vous ne définissiez
ce symbole, le compilateur ne l'écrit pas du tout dans votre assemblage, et les constantes se replient
avant cela. Rien n'est chargé, rien ne s'exécute, et aucune référence d'assemblage ne survit.

Asserté plutôt que promis — [la garantie d'empreinte nulle](zero-footprint.fr.md) dit exactement ce
que le test établit et ce qu'il n'établit pas.

## Pourquoi l'argument de catégorie vaut-il tout ça ? Rien ne le lit.

Précisément parce que rien ne le lit.

Un **identifiant** faux finit par remonter : la suppression cesse de correspondre et l'avertissement
revient. Une **catégorie** fausse n'a aucun destin — la ligne compile, l'avertissement est toujours
tu, et le fichier prétend désormais une catégorie que l'éditeur n'emploie pas. Aucun build n'échoue,
aucun test ne rougit, aucun outil ne signale.

Une erreur sans symptôme n'est pas une petite erreur ; c'est une erreur qu'on ne peut pas trouver.
C'est le registre qui se dégrade, et la première personne à s'y fier — cherchant toutes les
suppressions `"Major Code Smell"` avant une montée de version — obtient une réponse silencieusement
incomplète.

## Ne puis-je pas simplement écrire mon propre fichier de constantes ?

Si, et pour trente suppressions sur cinq règles, vous devriez sans doute.

Ce que cela ne vous donne pas, c'est d'où viennent les valeurs. `"Major Code Smell"` a été tapé par
quelqu'un, depuis un instantané — un billet de blog, le *Supprimer → Dans la source* d'un IDE, un
autre fichier. Être cohérent sur une valeur fausse n'est pas la même chose qu'avoir raison. Et cela ne
sait pas non plus quand une règle est retirée.

[Les alternatives](alternatives.fr.md) comparent les deux sérieusement, en disant où passe la ligne.

## Pourquoi pas simplement un analyseur qui valide les chaînes ?

Cette bibliothèque en livre un, et c'est délibérément la plus petite moitié.

Une vérification sur une chaîne ne peut juger que les chaînes qu'elle reconnaît.
`[SuppressMessage("Usage", "S1144")]` ne correspond à aucune règle décrite par un catalogue — alors,
catégorie fausse, ou règle d'un analyseur non catalogué ? Rien ne peut le dire, et un analyseur qui
devinerait signalerait un faux positif pour chaque analyseur non reflété. Il se tait donc, ce qui est
correct et n'est pas une solution.

Une constante n'a pas ce problème : soit elle résout, soit c'est une erreur de compilation. La
validation est celle du compilateur C#, et elle a toujours été là.

## Est-ce que ça marche avec `#pragma warning disable` ?

Non, et cela n'arrivera jamais. La directive prend un jeton identifiant nu, pas une expression : aucune
position ne pourrait accueillir une constante. C'est la grammaire de C#, pas une fonctionnalité
manquante.

Idem pour les clés de gravité d'`.editorconfig` — du texte brut, entièrement hors du modèle de
compilation.

Si l'essentiel de vos suppressions sont des `#pragma`,
[quand ne pas s'en servir](when-not-to-use.fr.md) le dit franchement.

## Ai-je besoin du paquet d'analyseurs ?

Pas pour la garantie. Une règle mal orthographiée est une erreur de compilation parce que
`SonarRule.S1144.Id` est un membre que le compilateur résout — aucun analyseur n'intervient.

`DiagnosticCatalog.Analyzers` trouve les suppressions que vous n'avez **pas** encore converties,
attrape une paire nommant deux règles différentes, et propose les correctifs. C'est une aide à la
migration plutôt que le mécanisme — et il n'a aujourd'hui aucune version sur nuget.org.

## Pourquoi `dcat` est-il un outil séparé plutôt qu'un générateur de source ?

Un générateur de source tourne dans le build de chaque consommateur, ce qui est le mauvais endroit
pour quelque chose qui télécharge un paquet NuGet et construit des analyseurs tiers.

La génération se fait une fois, dans le dépôt qui publie le catalogue, et sa sortie est commitée et
relue. C'est ce qui fait qu'une recatégorisation est quelque chose qu'un humain lit dans une pull
request plutôt qu'une chose qui change silencieusement dans l'`obj/` de tout le monde.

## Un catalogue peut-il couvrir Visual Basic ?

Pas aujourd'hui. Construire un analyseur Visual Basic demande un Roslyn que le worker de descripteurs
ne transporte pas : `--language vb` refuserait donc après avoir téléchargé le paquet. La clé existe
pour que le refus soit explicite plutôt qu'une devinette.

## Pourquoi la date de génération est-elle une chaîne ?

Parce qu'un argument d'attribut doit être une constante de compilation, et qu'aucun type de date ne
peut en être une. Même raison que pour `Id` et `Category` en `const string`. Utilisez `yyyy-MM-dd`.

## Pourquoi un catalogue ne supprime-t-il jamais une règle retirée ?

Les constantes sont incorporées dans les assemblages de vos consommateurs à **leur** compilation. En
supprimer une casse leur recompilation avec un `CS0117` nu qui nomme un type, un membre manquant, et
n'explique rien.

Reportée en `[Obsolete]`, la même montée de version leur donne `CS0618` — qui nomme la règle et dit ce
qui s'est passé ([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.fr.md)).

## Pourquoi la tâche nocturne ouvre-t-elle une pull request au lieu de fusionner ?

Parce qu'un identifiant ou une catégorie qui a bougé en amont est un changement de **contrat publié**,
et que, rien ne validant la catégorie d'une suppression, une valeur fausse fusionnée sans relecture
resterait invisible aussi longtemps qu'elle existerait.

L'automatisation trouve le changement ; un humain l'accepte.

## Pourquoi les projets de `--solution` doivent-ils déclarer une propriété ?

Parce que deviner n'est pas assez juste. Mesuré sur ce dépôt : « référence `Microsoft.CodeAnalysis` »
correspond à neuf projets dont un est un analyseur ; « déclare une sous-classe de `DiagnosticAnalyzer` »
correspond à quatre dont un est un montage écrit pour *échouer* à la construction, un exige une façade
que l'hôte doit porter, et un ne charge pas en entier.

Un projet manqué, ce sont ses règles absentes ; une règle absente est indiscernable d'une règle
retirée ; et elles sont publiées en `[Obsolete]` — disant aux utilisateurs de cet éditeur quelque
chose de faux.

## Est-ce affilié à SonarSource, Microsoft ou StyleCop ?

Non. Les catalogues sont des miroirs non officiels, générés depuis les descripteurs des analyseurs
eux-mêmes. Ils ne sont ni affiliés, ni approuvés, ni supportés par aucun de ces projets. « Sonar » et
« SonarQube » sont des marques de SonarSource S.A.

Les *faits* des règles sont redistribués — identifiant, catégorie, lien d'aide, titre. La prose des
éditeurs ne l'est délibérément pas
([ADR-0011](../adr/0011-redistribute-rule-facts-only-never-the-vendors-prose.fr.md),
[ADR-0014](../adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.fr.md)).

## Puis-je l'utiliser sur .NET Framework ?

Oui. Les bibliothèques ciblent `netstandard2.0` et `net10.0`, et le plancher est plus qu'une
affirmation de compilation — la CI exécute la suite de tests sur le vrai CLR .NET Framework 4.7.2
([ADR-0001](../adr/0001-floor-the-libraries-on-net-framework-4-7-2.fr.md)).

## Où poser une question qui n'est pas ici ?

Le [gestionnaire de tickets](https://github.com/Reefact/diagnostic-catalog/issues). Une question qu'il
a fallu poser est d'ordinaire une page qu'il aurait fallu écrire.

---

<div align="center">
<a href="./troubleshooting.fr.md">← Dépannage</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./glossary.fr.md">Glossaire →</a>
</div>
