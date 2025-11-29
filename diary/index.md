---
layout: home
title: Home
---

# BudgetBuddy Development Blog

Willkommen beim Entwicklungsblog von BudgetBuddy - einer F# Full-Stack Anwendung zur persönlichen Finanzverwaltung.

## Über das Projekt

BudgetBuddy verbindet dein Bankkonto (Comdirect) mit YNAB (You Need A Budget) und kategorisiert Transaktionen automatisch mit einer regelbasierten Engine.

**Tech Stack:**
- **Frontend:** Elmish.React + Feliz + TailwindCSS
- **Backend:** Giraffe + Fable.Remoting
- **Datenbank:** SQLite + Dapper
- **Deployment:** Docker + Tailscale

## Blogposts

{% for post in site.posts reversed %}
### [{{ post.title }}]({{ post.url | relative_url }})
*{{ post.date | date: "%d.%m.%Y" }}* - {{ post.description | default: post.excerpt | strip_html | truncate: 150 }}

{% endfor %}

---

[Development Diary](./development) | [GitHub Repository](https://github.com/romansachse/BudgetBuddy)
