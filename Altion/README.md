## Problem to solve
### Manipulating (ordering in this case) a file that size extends the machine RAM memory and manipulating of that big file will cause OOM exception

## Flow
### Step 1
Split one big file int x (configured size) smaller files
### Step 2
Apply sort on that smaller files
### Step 3
Merge back small files into one big using k-way merge alg

## 3rd party libs and ideas references
* ShellProgressBar, MIT license, https://github.com/Mpdreamz/shellprogressbar to show the progress in the Console
* CommandLineParser, MIT License, https://github.com/commandlineparser/commandline to parse the command line arguments
* Bogus, MIT License, https://github.com/bchavez/Bogus to generate test data
* K-WAY merge inspired by Josef Ottosson article https://josef.codes/sorting-really-large-files-with-c-sharp/

## HowTo

### Please have a look in to the appsettings.json - there you can manipulate the generation anad sorting settings. Hopefuly they are self explaining

### altion --help will print option possibilities
### altion -g will generate test file with configured lines of data under configured location (reffer to appsettings.json)
### altion -s will will sort the data