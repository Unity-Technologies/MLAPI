# Contributing

Thank you for your interest in contributing to Unity Multiplayer Networking!

Here are our guidlines for contributing:

* [Code of Conduct](#coc)
* [Coding Standards](#standards)
* [Branching](#branch)
* [Ways to Contribute](#ways)
* [Issues and Bugs](#issue)
* [Feature Requests](#feature)
* [Improving Documentation](#docs)
* [Unity Contribution Agreement](#cla)
* [Pull Request Submission Guidlines](#submit-pr)

## <a name="coc"></a> Code of Conduct

Please help us keep Unity Netcode for GameObjects open and inclusive. Read and follow our [Code of Conduct](CODE_OF_CONDUCT.md).

## <a name="standards"></a> Coding Standards

Your contributions must follow the [Unity C# Coding Standards](CODING_STANDARDS.cs).

Our standard extends the [Microsoft Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/), which defines a number of rules not covered by [Unity C# Coding Standards](CODING_STANDARDS.cs).

Unity C# Coding Standards includes a subset of the most used rules as well as any additions and exceptions to the Microsoft Framework Design Guidelines (FDG).

If there is any disagreement between [Unity C# Coding Standards](CODING_STANDARDS.cs) and the [Microsoft Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/), Unity C# Coding Standards always wins. Exceptions to the FDG are marked with [FDG Exception].

## <a name="branch"></a> Branching

External pull requests has to be based on and target the ``develop`` branch.

## <a name="ways"></a> Ways to Contribute

There are many ways in which you can contribute to Netcode for GameObjects.

### <a name="issue"></a> Issues and Bugs

If you find a bug in the source code, you can help us by submitting an issue to our
GitHub Repository. Even better, you can submit a Pull Request with a fix.

### <a name="feature"></a> Feature Requests

You can request a new feature by submitting an issue to our GitHub Repository.

If you would like to implement a new feature then consider what kind of change it is:

* **Major Changes** that you wish to contribute to the project should be discussed first with other developers. We will have a more formal process for this soon. For now submit your ideas as an issue.

* **Small Changes** can be directly submitted to the GitHub Repository
  as a Pull Request. See the section about [Pull Request Submission Guidelines](#submit-pr).

### <a name="docs"></a> Documentation

We accept changes and improvements to our documentation. Just submit a Pull Request with your proposed changes as described in the [Pull Request Submission Guidelines](#submit-pr).

## <a name="cla"></a> Contributor License Agreements

When you open a pull request, you will be asked to enter into Unity's License Agreement which is based on The Apache Software Foundation's contribution agreement. We allow both individual contributions and contributions made on behalf of companies. We use an open source tool called CLA assistant. If you have any questions on our CLA, please submit an issue

## <a name="submit-pr"></a> Pull Request Submission Guidelines

We use the [Gitflow Workflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow) for the development of the Netcode for GameObjects. This means development happens on the **develop branch** and Pull Requests should be submited to it.

### Commit Message Guidelines
We have very precise rules over how our git commit messages can be formatted.  This leads to **more
readable messages** that are easy to follow when looking through the **project history**. We follow angular's message format.

#### **Commit Message Format**
Each commit message consists of a **header**, a **body** and a **footer**.  The header has a special
format that includes a **type**, a **scope** and a **subject**:

```
<type>(<scope>): <subject>
<BLANK LINE>
<body>
<BLANK LINE>
<footer>
```

The **header** is mandatory and the **scope** of the header is optional.

Any line of the commit message cannot be longer 100 characters! This allows the message to be easier
to read on GitHub as well as in various git tools.

The footer should contain a [closing reference to an issue](https://help.github.com/articles/closing-issues-via-commit-messages/) if any.

If the change isn't backwards compatible. You have to disclose that in the footer like this. Here is an example:
```
perf(networked-vars): Improved performance by removing duplex functionality

BREAKING CHANGE: Removes duplex functionality from network variables in order to improve performance
```
Samples:

```
docs(changelog): update changelog to beta.5
```
```
fix(release): need to depend on latest rxjs and zone.js

The version in our package.json gets copied to the one we publish, and users need the latest of these.
```

### Revert
If the commit reverts a previous commit, it should begin with `revert: `, followed by the header of the reverted commit. In the body it should say: `This reverts commit <hash>.`, where the hash is the SHA of the commit being reverted.

### Type
Must be one of the following:

* **build**: Changes that affect the build system or external dependencies (example scopes: msbuild, xUnit)
* **ci**: Changes to our CI configuration files and scripts (example scopes: AppVeyor, xUnit)
* **docs**: Documentation only changes
* **feat**: A new feature
* **fix**: A bug fix
* **perf**: A code change that improves performance
* **refactor**: A code change that neither fixes a bug nor adds a feature
* **style**: Changes that do not affect the meaning of the code (white-space, formatting, missing semi-colons, etc)
* **test**: Adding missing tests or correcting existing tests

Always write a clear log message for your commits. One-line messages are fine for small changes, but bigger changes should look like this:

    $ git commit -m "A brief summary of the commit
    > 
    > A paragraph describing what changed and its impact."
