# End-to-end walk-through

`demo.py` drives a real browser through the 12 demo items of the client brief against a running site (67 checks).
It is optional: it needs Python 3, `pip install playwright` and `playwright install chromium`.

```bash
scripts/reset-db.sh          # start from the demo data
scripts/run.sh               # in another terminal
python3 tests/e2e/demo.py    # BASE=http://localhost:5080 by default; SHOTS=<folder> for screenshots
```

The script changes data (it lends items, imports files), so reset the data afterwards.
