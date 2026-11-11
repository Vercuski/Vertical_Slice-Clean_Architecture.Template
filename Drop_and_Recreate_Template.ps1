<#
.SYNOPSIS
    Rebuilds and reinstalls the Vertical Slice + Clean Architecture `dotnet new` template locally.

.DESCRIPTION
    Use this during template development to make sure `dotnet new` runs the latest local build:
      1. Lists the installed template packages.
      2. Uninstalls the Vercuski.Vertical_Slice-Clean_Architecture.Templates package.
      3. Deletes every subfolder of %USERPROFILE%\.templateengine to clear the template engine cache.
         This affects all installed templates, not just this one.
      4. Uninstalls any "folder install" of .\source.
      5. Packs template\Vercuski.Vertical_Slice-Clean_Architecture.Templates.csproj into .\artifacts.
      6. Installs the newest .nupkg found in .\artifacts.
      7. Prints the template's help so you can confirm the --data-access option is listed.

    Uninstall steps print an error if there is nothing to uninstall; this is expected.

.NOTES
    Run from the repository root, since every path in the script is relative to it.
#>

echo ""
echo ""
echo "***************************"
echo "* List installed packages *"
echo "***************************"
dotnet new uninstall          # lists installed packages; note any Vercuski.Vertical_Slice-Clean_Architecture.Templates or ...\source entries

echo ""
echo ""
echo "*******************************************"
echo "* Uninstall previous Vertical Slice + Clean Architecture Template *"
echo "*******************************************"
dotnet new uninstall Vercuski.Vertical_Slice-Clean_Architecture.Templates

echo ""
echo ""
echo "************************************************"
echo "* Empty $env:USERPROFILE\.templateengine *"
echo "************************************************"
Get-ChildItem "$env:USERPROFILE\.templateengine" -Directory | Remove-Item -Recurse -Force

echo ""
echo ""
echo "******************************************"
echo "* Uninstall any 'folder install' templates *"
echo "******************************************"
dotnet new uninstall "$PWD\source"   # only if a folder install is listed

echo ""
echo ""
echo "*********************************"
echo "* Packing Vertical Slice + Clean Architecture Template *"
echo "*********************************"
dotnet pack template/Vercuski.Vertical_Slice-Clean_Architecture.Templates.csproj -c Release -o artifacts

echo ""
echo ""
echo "************************"
echo "* Install new template *"
echo "************************"
dotnet new install (Get-ChildItem artifacts\Vercuski.Vertical_Slice-Clean_Architecture.Templates.*.nupkg | Sort-Object LastWriteTime | Select-Object -Last 1).FullName

echo ""
echo ""
echo "*********************************************************************************************************"
echo "* Test if new template s installed you should see '-da, --data-access <Both|Dapper|EfCore>' in the text *"
echo "*********************************************************************************************************"
dotnet new Vertical_Slice-Clean_Architecture --help   # should list:  -da, --data-access <Both|Dapper|EfCore>