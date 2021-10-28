#!/bin/bash
echo "you must source the script. executing it won't work"
conda activate reviewer-finder
python -m nltk.downloader stopwords
python -m nltk.downloader universal_tagset
python -m spacy download en_core_web_sm
