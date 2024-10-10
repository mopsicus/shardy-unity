# 💬 Overview

This package is a Unity client for Shardy. It provides RPC framework with simple user-friendly API: requests, commands and subscribe for communication with Shardy microservices. 

[Shardy](https://github.com/mopsicus/shardy) is a server framework for online games and applications for Node.js. It provides the basic development framework for building microservices solutions: mobile, social, web, multiplayer games, realtime applications, chats, middleware services, etc.
 
The main goal of Shardy is to give simple free solution for building almost any kind of online project. 💪

# 🥷 For whom and why

Shardy grew out of a few scripts for internal use, as is often the case. It was mostly used as a local game backend. It was inspired by a rather famous old project - Pomelo. The package format and general concept of the RPC framework were taken from there. New features were added - the code became more complex. In the end, it was decided to rewrite everything, add WebSocket support and share it.

I made it alone, primarily for my own needs, after several iterations of rewriting the entire codebase 😄 I'm not claiming that Shardy will work for your project, but it's a really simple solution for adding multiplayer to your game.

If you have minimal knowledge of Node.js and TypeScript, you can easily [launch your own service on Shardy](https://github.com/mopsicus/shardy-template) and use it as a server for your game or application. 

> [!NOTE]
> This package doesn't use any third-party libraries for its work. 😎

# 🚀 Why should I use it

Start your project with Shardy and rest assured:

- **easy to use:** work with a user-friendly API and don't worry about how it works under the hood
- **mobile platforms:** build your app for Android, iOS and WebGL from one network codebase
- **fast & lightweight:** core without any 3rd party libs, pure C#
- **full docs:** Shardy provides good docs with all necessary sections and API references, also all code is coverged by comments ✌️

# 🗓️ Plans

The plans are truly grand! It is to create an ecosystem for developers who will be able to build their game backend out of existing Shardy services like bricks, and compose mobile clients from Shardy modules.

First and foremost:
- base helpers
- binding UI
- event bus
- scene/screen manager
- localization
- sound manager
- ads manager

All of these modules will be as separate packages. The list is endless.

Also in the plans: writing tutorials, more examples and open source game. And don't forget to [read about server-side plans](https://github.com/mopsicus/shardy/blob/main/Documentation~/overview.md#-plans) 🌐