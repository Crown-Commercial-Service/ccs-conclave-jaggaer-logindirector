GCA Login Director Service
===========

Overview
--------
This is the code for the Government Commercial Agency's (_GCA_)
Login Director service (_LD_), which serves to link Public Procurement
Gateway (_PPG_) accounts and external eSourcing (_Jaegger_) accounts
together so that each system recognises users correctly.

Technology Overview
---------
The project is implemented as a .NET 10 web application, implemented using NuGet.

The core technologies for the project are:

* .NET 10
* .NET OAuth middleware
* [Razor][] for templating
* [NPM][], [Gulp][] and [Rollup][] for CSS/JS management
* [NUnit][] for unit testing
* AWS for secret management

Building and Running Locally
----------------------------
To run the application locally, you simply need to run the core logindirector project.

In order to generate front-end assets you can either manually run _**npm run generate-assets**_, or build the project - the assets are automatically generated at build time.

You will need to be supplied with a local secrets file (`secrets.json`) to enable the project to run, which can be supplied by any member of the development team.

Once the application has started it can be accessed in a web browser using the URL https://localhost:2021/. This URL will automatically launch when the application is run.

Branches
--------
When picking up tickets, branches should be created using the **_feature/*_** format.

When completed, these branches should be pull requested against _**develop**_ for review and approval.  _**develop**_ is then built out onto the **Development** environment.

Currently only the **Development** and **Production** environments are in use. As such, when releases are ready for deployment to **Production**, the **develop** branch should be pull requested against the _**main**_ branch for review and approval.  This branch should then be built out to **Production**.

Assets
------
Assets are stored in the `wwwroot/assets` directory.
We then use [Gulp][] and [Rollup][] to compile assets into the `wwwroot/public/assets` directory.

The design of the app is closely based on the [GOV.UK Design System][] with some minor GCA-related variations.
Therefore, we make use of [GOV.UK Frontend][] as the main source for our Stylesheets and Javascript.

### Building assets

To build the assets, you need to run:

```bash
$ npm run build-assets
```

If you want the assets (the `scss` and the JavaScript) to be automatically updated when you change them, in a separate terminal run:

```bash
$ npm run watch-assets
```

### Types of assets

The application has four types of assets:
- [Fonts](#fonts)
- [Images](#images)
- [Stylesheets](#stylesheets)
- [JavaScript](#javascript)

#### Fonts

These font files are from GOV.UK Frontend and are added into `wwwroot/public/assets/fonts` when we build the assets.
As these are automatically added you should not need to add and change the fonts for the project.

#### Images

Images come from the local files and from GOV.UK Frontend.

If you need to add an image, it should be done in `wwwroot/assets/images`.
When the assets are built, these images are combined with the GOV.UK Frontend images and added to the `wwwroot/public/assets/images` directory.

#### Stylesheets

We use [Sass][] to create our CSS.
Stylesheets must be added to the `wwwroot/assets/stylesheets` directory.

The entry point for the `scss` is `wwwroot/assets/stylesheets/application.scss` so any additional `scss` must be referenced (directly or through other files) in that file.
When the assets are built, the `scss` will be compiled into a single `css` file: `wwwroot/public/assets/application.css`.

#### JavaScript

We write our JavaScript using ECMAScript `imports` and `exports` so we use Rollup to compile these into a single `commonjs` file.
JavaScripts must be added to the `wwwroot/assets/javascript` directory.

The entry point for the JavaScript is `wwwroot/assets/javascript/application.mjs` so any additional JavaScript must be referenced (directly or through other files) in that file.
When the assets are built, the JavaScripts will be compiled into a single `js` file: `wwwroot/public/assets/application.js`.

[Razor]: https://learn.microsoft.com/en-us/aspnet/core/razor-pages/?view=aspnetcore-10.0&tabs=visual-studio
[NPM]: https://www.npmjs.com/
[NUnit]: https://nunit.org/
[Gulp]: https://gulpjs.com/
[Rollup]: https://rollupjs.org/
[GOV.UK Design System]: https://design-system.service.gov.uk/
[GOV.UK Frontend]: https://github.com/alphagov/govuk-frontend
[Sass]: https://sass-lang.com/