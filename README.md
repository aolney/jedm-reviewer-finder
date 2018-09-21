# jedm-reviewer-finder
Find journal reviewers using keywords; single page app; self-contained

The project has two pieces:

- A Jupyter Notebook for prototyping the method and generating a conformant data model
- A Fable single page appliciation for loading the data model and running queries

## Jupyter Notebook
Uses [SOS kernel](https://vatlab.github.io/sos-docs/) and multiple languages plugins (F#, Python, Bash) as well as libraries ([pke](https://github.com/boudinfl/pke), [GROBID](https://github.com/kermitt2/grobid)).
These are required dependencies.
If you do not wish to work with the notebook, you can open it in a text editor and then chain scripts
written in the above langauges together. 
This is possible because all data passed between languages hits the disk.

The notebook describes the data sources used. 
Ostensibly any scientific publication in PDF form would work.
The output data model is JSON wrapped in JS functions.
These are consumed by the client (copy to the client directory).

## Fable client

Uses [Fable](http://fable.io/) in a fairly simple app. See the README in that directory.
Deploys directly to gh-pages.
