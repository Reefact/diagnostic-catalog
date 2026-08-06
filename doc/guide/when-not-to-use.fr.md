# Quand ne pas s'en servir

🌍 **Langues :**  
🇬🇧 [English](./when-not-to-use.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque hésite à adopter. Écrite pour vous en dissuader là où il le faut — une bibliothèque
qui ne plaide que pour elle-même est une bibliothèque qu'il faut évaluer deux fois.

## La réponse courte

Prenez ceci quand les **suppressions sont porteuses** — quand il y en a assez, sur une durée assez
longue, pour qu'une seule d'entre elles silencieusement fausse soit un vrai coût. En dessous de cette
ligne, la cérémonie n'est pas remboursée.

## Les cas où cela n'en vaut pas la peine

### Une poignée de suppressions dans un seul projet

Dix suppressions, un dépôt, une équipe, aucune montée d'analyseur en vue. Vous les lisez toutes en
une minute, et une mauvaise catégorie ne coûte rien que vous remarqueriez un jour.

La bibliothèque n'est pas nuisible ici — elle coûte une `PackageReference` et rien à l'exécution —
mais elle résout un problème que vous n'avez pas. Adoptez-la quand le nombre grandira, ce que vous
pouvez décider plus tard sans pénalité : rien de la forme littérale n'a besoin d'être défait d'abord.

### Vous supprimez avec `#pragma`, pas avec des attributs

```csharp
#pragma warning disable S1144
```

C'est **hors d'atteinte, définitivement**. La directive prend un jeton identifiant nu, pas une
expression : il n'existe aucune position où une constante pourrait être substituée. Aucune version de
cette bibliothèque n'y changera rien ; c'est la grammaire de C#.

Si votre base de code supprime surtout de cette façon, la couverture par attributs vous donnera
l'impression de passer à côté — parce que, pour vous, c'est le cas.

### Vous réglez la gravité dans `.editorconfig` plutôt que de supprimer sur un site

```ini
dotnet_diagnostic.S1144.severity = none
```

Hors d'atteinte également, pour une raison voisine : les clés `.editorconfig` sont du texte brut lu
entièrement hors du modèle de compilation C#. Un projet qui éteint des règles globalement et ne
supprime jamais sur un site n'a rien ici à faire vérifier.

### Vos règles sont localisées

Si les titres et messages de votre analyseur sont des `LocalizableString` adossés à des resx, ce
texte ne peut pas être une `const` et tombe hors de ce modèle. L'identifiant et la catégorie, eux, le
peuvent — c'est l'axe que cette bibliothèque couvre — mais si la valeur que vous vouliez référencer
fortement est le message traduit, les fichiers de ressources restent le bon outil et ceci n'en est
pas une alternative.

### Vous voulez que la suppression soit *justifiée*, pas seulement bien orthographiée

Ceci vérifie qu'une suppression est **structurellement cohérente** : qu'elle nomme une vraie règle,
de façon cohérente. Cela n'a aucune opinion sur le fait que supprimer cette règle à cet endroit était
une bonne idée, et n'en aura jamais. Ce jugement, c'est à quoi sert `Justification`, et à quoi sert
la revue de code.

Une équipe dont le vrai problème est « les gens suppriment des choses qu'ils devraient corriger »
n'est aidée par rien de tout ceci.

## Les cas où cela en vaut la peine

Énoncés en miroir, pour que la ligne soit visible des deux côtés :

* **Une base de code qui supprime couramment.** Des centaines de sites, plusieurs analyseurs,
  plusieurs dépôts. Une catégorie fausse est invisible ; cent le sont aussi, et le registre devient
  inutilisable.
* **Un chemin de montée d'analyseur qui doit faire remonter renommages et retraits.** Une montée
  d'éditeur qui annule silencieusement une suppression est exactement la défaillance que le report en
  `[Obsolete]` convertit en avertissement de build nommant la règle.
* **Un auteur d'analyseur qui veut que ses règles soient référencées symboliquement.** Alimenter
  votre propre `DiagnosticDescriptor` depuis votre propre catalogue rend exacte par construction la
  catégorie que vos utilisateurs écrivent — chose qu'un miroir tiers ne peut jamais offrir.
* **Une équipe qui standardise un jeu de règles entre dépôts.** Le catalogue est le vocabulaire
  partagé, et il est vérifié par le compilateur dans chaque dépôt plutôt que convenu dans un wiki.

## Les coûts, dits franchement

Pas des « compromis ». Des coûts.

| Coût | Taille |
| --- | --- |
| Une `PackageReference` par projet qui écrit des suppressions. | Une ligne. |
| Des lignes de suppression plus longues — `SonarRule.S1144.Category` contre `"Major Code Smell"`. | Réel, et la raison d'être des alias. |
| Votre catalogue est l'instantané d'une version d'éditeur. | Il se périme, et seuls `dcat validate` ou une régénération le diront. |
| Adopter sur une base existante signale toutes les suppressions littérales d'un coup, si vous prenez les analyseurs. | Gérable avec une rampe de gravité ; ingérable si vous prenez le paquet sous `TreatWarningsAsErrors` sans rien changer d'autre. |

Ce qui n'est **pas** un coût, et qu'on suppose souvent en être un : l'exécution. Il n'y en a pas.
L'attribut est `[Conditional("CODE_ANALYSIS")]` et n'est pas émis ; les constantes se replient avant
cela. Aucune dépendance n'atteint votre application, et un test l'asserte.

## Que faire à la place, si la réponse est non

* **Gardez les littéraux et écrivez la catégorie depuis le descripteur.** Si vous possédez
  l'analyseur, assurez-vous au moins que la valeur que vous collez vient de `DiagnosticDescriptor` et
  non d'une documentation à son sujet. C'est là que commence l'essentiel de la dérive que cette
  bibliothèque empêche.
* **Faites un grep avant une montée de version.** Une recherche textuelle sur les identifiants que
  vous supprimez, confrontée aux notes de version de l'éditeur, attrape les retraits. C'est manuel et
  cela n'attrape pas les catégories, mais c'est déjà quelque chose.
* **Lisez [les alternatives](alternatives.fr.md).** La page suivante compare ceci aux autres façons
  de résoudre le même problème, y compris ne rien faire.

---

<div align="center">
<a href="./zero-footprint.fr.md">← La garantie d'empreinte nulle</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./alternatives.fr.md">Les alternatives →</a>
</div>
