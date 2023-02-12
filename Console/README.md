GARbro.Console
===============

The original feature that was abandoned is now brought back to life. Because we need a good CLI to make GARbro able to be integrated into various applications.

### Usage

`GARbro.Console.exe <command> [<switches>...] <archive_name>`

###### Commands:

| Command | Description                                                  |
| ------- | ------------------------------------------------------------ |
| i       | Identify archive format                                      |
| f       | Display supported formats. This prints a list of all formats GARbro can recognize. |
| l       | List contents of archive                                     |
| x       | Extract files from archive                                   |

###### Switches:

| Switch         | Description                                                  |
| -------------- | ------------------------------------------------------------ |
| -o <Directory> | Set output directory for extraction                          |
| -f <Filter>    | Only process files matching the regular expression <Filter>  |
| -if <Format>   | Set image output format  (e.g. 'png', 'jpg', 'bmp'). This converts all image files to the specified format. Caution: conversion might reduce the image quality and transparency can be lost (depending on the output format). Use the `-ocu` switch to skip common image formats. |
| -y             | Do not prompt if the same file exist. Will always overwrite. |
| -ca            | Convert audio files to wav format; without this switch the original format is retained |
| -na            | Skip audio files                                             |
| -ni            | Skip image files                                             |
| -ns            | Skip scripts                                                 |
| -aio           | Adjust image offset                                          |
| -ocu           | Set -if switch to only convert unknown image formats.<br />The default behavior of `-if` is to convert all images to the specified format. This might not desirable because it reduces the image quality or transparency information can be lost. With the `-ocu` switch only uncommon formats are converted - for example jpg and png files are kept as is, while proprietary formats are converted. |