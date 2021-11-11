Clear-Host

$source = 'C:\prg\myProjects\GitHub\YahooQuotesApi\YahooQuotesApi\bin\Release'
$dest   = 'C:\prg\Nuget\repo'
$file   = "YahooQuotesApi.5.0.0.nupkg"
Copy-Item "$source\$file" -Destination "$dest"

$cache   = 'C:\Users\david\.nuget\packages\yahooquotesapi'
Remove-Item -Path "$cache" -Recurse

Write-Output "Complete!"
