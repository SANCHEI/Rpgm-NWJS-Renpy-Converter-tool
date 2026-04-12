# GitHub Upload Instructions

## Prerequisites
1. GitHub account (https://github.com)
2. Git installed on your computer

## Step 1: Create a New Repository on GitHub

1. Go to https://github.com/new
2. Repository name: `RpgmvpConverterWinForms`
3. Description: `RPGMVP/PNG_ to PNG Converter - Windows desktop application`
4. Select: Public (or Private if preferred)
5. Do NOT initialize with README (we have one)
6. Click "Create repository"

## Step 2: Initialize Git Locally

Open Command Prompt in your project folder:

```batch
cd D:\123123\converter_WinForms

git init
git config user.name "Your Username"
git config user.email "your.email@example.com"
```

## Step 3: Create .gitignore

Create a file called `.gitignore`:
```
bin/
obj/
*.exe
*.dll
*.user
*.suo
.vs/
```

## Step 4: Add Files and Commit

```batch
git add .
git commit -m "Initial commit - RPGMVP Converter WinForms application"
```

## Step 5: Connect to GitHub and Push

Replace `YOUR_USERNAME` with your GitHub username:

```batch
git remote add origin https://github.com/YOUR_USERNAME/RpgmvpConverterWinForms.git
git branch -M main
git push -u origin main
```

## Step 6: Enable GitHub Pages (for nice README)

1. Go to your repository on GitHub
2. Settings > Pages
3. Source: Deploy from a branch
4. Branch: main, / (root)
5. Click Save

Your README will be visible at: `https://YOUR_USERNAME.github.io/RpgmvpConverterWinForms/`

## Step 7: Create a Release

1. Go to your repository
2. Click "Releases" > "Draft a new release"
3. Tag version: `v1.0.0`
4. Release title: `RPGMVP Converter v1.0.0`
5. Upload your built `.exe` from the `bin` folder
6. Click "Publish release"

## Optional: Add Badges

Edit your README.md to add status badges. You can generate them at:
https://shields.io

Example:
```markdown
![Build](https://github.com/YOUR_USERNAME/RpgmvpConverterWinForms/actions/workflows/build.yml/badge.svg)
![Release](https://img.shields.io/github/v/release/YOUR_USERNAME/RpgmvpConverterWinForms)
```

## GitHub Actions (Auto-Build)

Create `.github/workflows/build.yml`:
```yaml
name: Build

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    - name: Build
      run: |
        ./build_winforms.bat
    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: RpgmvpConverterWinForms
        path: bin/*.exe
```

This will automatically build your project on every push!
