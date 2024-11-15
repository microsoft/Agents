# Policies: Repository Branching

The git branching and workflow strategy we will be using is mostly in line with [OneFlow](https://www.endoflineblog.com/oneflow-a-git-branching-model-and-workflow) with some variations called out below.

Moving forwards in this document every time a naming rule regarding branches is mentioned, it would be assumed that all text between `/` should be `dash-case`.

## Main branch

`main` is the main default branch that lives forever and should never be force pushed to. The main branch must always be in a working state where CI builds succeed (e.g. build, analyze, and tests passing). Everytime a release process is started, a tag with corresponding version will be created, as well as a release branch. All feature and fix branches are intentionally short lived and should be removed once they are no longer needed.

## Feature branches (internal)
For individual work, people should create branches off main and keep them in both their local and the remote repository (for redundancy in case of local storage failure).

The name for individual branches should be `users/<contributor-alias>/<feature-name>`.

```bash
# switch to your local main
git checkout main
# update your local main branch with what is in the main repo
git pull origin main
# create your local feature branch
git checkout -b users/johndoe/feature-example
```

If there are a set of people that need to collaborate on the same set of changes before they can go into main then a feature branch can be pushed to the main repository for sharing. Collaborators can either work together to push changes to that branch or submit pull requests against it until it is ready to go to main. Once the feature work is ready the changes should be merged on top of main and a pull request submitted to the main branch in the main repository. Once the feature work pull request is complete then the branch should be deleted from the main repository.

Collaborative feature branches pushed to the main repo should be named like `feature/<feature-name>`.

Either individual or collaborative, once the work is ready a pull request should be submitted to the main repository. Upon approval, the merge process will automatically **squash and merge** the changes on top of main.

## External work should happen in Forks

In order to help reduce the clutter of branches in the main repo as well as to enable a common workflow for both contributors and community members with different permissions people should create forks of the main repository and work in them. Once work is ready in the fork then a pull request can be submitted back to the main repository.

See the next few sections for some simple getting started instructions for using forks but for more detailed documentation from github see working-with-forks.

### Clone forked repo

After you have created your own fork by clicking the fork button on the main repo you can use the following commands to clone and set up your local repo.

```bash
# clone your forked repo locally which will setup your origin remote
git clone https://github.com/<your-github-username>/azure-sdk-for-<lang>.git
# add an upstream remote to the main repository.
cd azure-sdk-for-<lang>
git remote add upstream https://github.com/Azure/azure-sdk-for-<lang>.git
git remote -v
# origin https://github.com/<your-github-username>/azure-sdk-for-<lang>.git (fetch)
# origin https://github.com/<your-github-username>/azure-sdk-for-<lang>.git (push)
# upstream https://github.com/Azure/azure-sdk-for-<lang>.git (fetch)
# upstream https://github.com/Azure/azure-sdk-for-<lang>.git (push)
```
After you have ran those commands you should be all setup with your local cloned repo, a remote for your forked repo called origin, and a remote for the main repo called upstream.

### Sync local and forked repo with latest changes from the main repo
Working in a fork is highly recommended so you should avoid committing changes directly into main and you can use it to sync your various repos. The instructions in this section assume you are using main as your syncing branch.

```bash
# switch to your local main
git checkout main
# update your local main branch with what is in the main repo
git pull upstream main --ff-only
# update your forked repo's main branch to match
git push origin main
```
At this point all three of your repos - local, origin, and upstream - should all match and be in sync.

Note that in order to ensure that we don’t accidently get our local or origin main out of sync we use the --ff-only (fast-forward only) option which will fail if you have any commits that aren’t already in the main repo. If you ever get into this state the easiest thing to do is to force reset your local main branch.
```bash
# Warning: this will remove any commits you might have in your local main so if
# you need those you should stash them in another branch before doing this
git reset --hard upstream/main
# If you also have your forked main out of sync you might need to use the force option when you push those changes
git push origin main -f
```
### Creating a branch and pushing it to your fork
After your local main branch is in-sync with the upstream main you can now create a branch and do work.
```bash
git checkout <branch-name>
# Make changes
# Stage changes for commit
git add <file-path> # or * for all files
git commit
git push origin <branch-name>
```
At this point you should be able to go to the main repository on github and see a link to create a pull request with the changes you pushed.

Tip: Some folks like to quickly stage and commit with a simple message at the same time you can use the following command for that.
```bash
git commit -am "Commit message"
```
Note that `-a` means commit all files that have changes so be sure to not have any other modified files in your working directory. The -m allows you to pass a commit message at the command line and is useful for quick commits but you might want to use your configured editor for better commit messages when pushing changes you want to be reviewed and merged into the main repo.

### Merge changes on top of latest main
If you have changes that you have been working on and you need to update them with the latest changes from main, you should do the following commands after you have sync’ed your local main.
```bash
git checkout <branch-name>
# If there are any merge conflicts you will need to resolve them now.
git merge main
# Assuming there were new changes in main since you created your branch originally rebase will rewrite the commits and
# as such will require you to do a force push to your fork which is why the '-f' option is passed.
git push origin <branch-name> -f
```
Tip: if you want to squash changes you can add the `-i` to the rebase command for interactive mode to select which commits you want to squash, see interactive mode for information.

## Release tagging
For each package we release there will be a unique git tag created that contains the name and the version of the package to mark the commit of the code that produced the package.

The tag will be autogenerated by [nbgv](https://github.com/dotnet/Nerdbank.GitVersioning/blob/main/doc/nbgv-cli.md#preparing-a-release).

Note: Our release tags should be considered immutable. Avoid updating or deleting them after they are pushed. If you need to update or delete one for some exceptional case, please contact the engineering system team to discuss options.

## Release branches
While `main` will always be in a buildable state it will not necessarily always represent the state of the latest official published packages. To figure out the state of the code for a given released package you need to use the tag for that released package and refer to the latest state in said branch.

The naming for release branches should be `release/<vX.X.X>`

For more information on our release mechanism refer to [nbgv](https://github.com/dotnet/Nerdbank.GitVersioning/blob/main/doc/nbgv-cli.md#preparing-a-release).

## Fixes
We have a **fix forwad strategy** regarding hotfixes. This means that in the case we may need to quickly address a problem in production we will only patch the latest version.

You should use your usual workflow and the changes should be made and the version number incremented based on our versioning guidance for the package. After merging the fix in `main` you should `cherry-pick` said change into latest release branch.
