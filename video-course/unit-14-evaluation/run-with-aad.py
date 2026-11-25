"""Run the existing local evaluation harness with AAD as its sole judge credential.

Set the judge endpoint/version and sign in with Azure CLI first. Arguments are forwarded
to evaluation.assert_demo.run. Only this process and its children are affected.
"""
import os
from pathlib import Path
import runpy
import sys

root = Path(__file__).resolve().parents[2]
os.chdir(root)
sys.path.insert(0, str(root))
for name in ("AZURE_API_KEY", "AZURE_OPENAI_API_KEY", "OPENAI_API_KEY", "AZURE_AD_TOKEN", "AZURE_OPENAI_AD_TOKEN"):
    os.environ.pop(name, None)
os.environ["ASSERT_AZURE_USE_AAD"] = "1"
os.environ["PYTHON_DOTENV_DISABLED"] = "1"
sys.argv[0] = "evaluation.assert_demo.run"
runpy.run_module("evaluation.assert_demo.run", run_name="__main__")
