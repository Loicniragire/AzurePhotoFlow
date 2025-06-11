# Local Setup

This project includes a small Python script used to export the CLIP model to TorchScript format. The script depends on packages such as **torch** and **transformers**. To keep these dependencies isolated, create a Python virtual environment.

```bash
# Create the virtual environment and install requirements
python scripts/setup_venv.py --path .venv

# Activate the environment (Linux/macOS)
source .venv/bin/activate
# Windows
#.venv\Scripts\activate
```

Once activated, you can run the helper script:

```bash
python scripts/export_clip_trace.py --output models/clip_vision_traced.pt
```
