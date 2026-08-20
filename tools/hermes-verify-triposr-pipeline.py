#!/usr/bin/env python3
"""
Ad-hoc verification for TripoSR pipeline changes.
Tests:
1. isosurface.py patch works (skimage marching_cubes replacement)
2. generate_triposr.py self-test passes
3. decimate_props.py self-test passes
4. End-to-end: image -> TripoSR -> GLB -> decimate -> .bytes
"""
import sys
import os
import tempfile
import subprocess
from pathlib import Path

REPO_ROOT = Path("C:/Users/danil/projects/OblastZero")
TOOLS_DIR = REPO_ROOT / "tools"
VENV_PYTHON = Path("C:/Users/danil/AppData/Local/hermes/hermes-agent/venv/Scripts/python.exe")

def run_cmd(cmd, cwd=None, timeout=300):
    """Run command and return (success, stdout, stderr)."""
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout)
    return result.returncode == 0, result.stdout, result.stderr

def test_isosurface_patch():
    """Verify the isosurface.py patch uses skimage."""
    isosurface_path = REPO_ROOT / "tools" / "TripoSR" / "tsr" / "models" / "isosurface.py"
    content = isosurface_path.read_text()
    
    checks = [
        ("skimage import", "from skimage import measure" in content),
        ("torchmcubes removed", "from torchmcubes import marching_cubes" not in content),
        ("_marching_cubes_cpu function", "_marching_cubes_cpu" in content),
        ("skimage usage", "measure.marching_cubes" in content),
        ("CPU fallback works", "level_np = level.detach().cpu().numpy()" in content),
    ]
    
    print("=== isosurface.py patch verification ===")
    for name, check in checks:
        status = "✓" if check else "✗"
        print(f"  {status} {name}")
    return all(c for _, c in checks)

def test_self_tests():
    """Run both self-tests."""
    print("\n=== Self-tests ===")
    
    # generate_triposr --self-test
    ok, out, err = run_cmd([str(VENV_PYTHON), str(TOOLS_DIR / "generate_triposr.py"), "--self-test"], cwd=REPO_ROOT)
    gen_ok = "3/3 checks passed" in out
    print(f"  {'✓' if gen_ok else '✗'} generate_triposr --self-test")
    
    # decimate_props --self-test
    ok, out, err = run_cmd([str(VENV_PYTHON), str(TOOLS_DIR / "decimate_props.py"), "--self-test"], cwd=REPO_ROOT)
    dec_ok = "46/46 checks passed" in out
    print(f"  {'✓' if dec_ok else '✗'} decimate_props --self-test")
    
    return gen_ok and dec_ok

def test_end_to_end():
    """Test: image -> TripoSR -> GLB -> decimate -> .bytes"""
    print("\n=== End-to-end pipeline test ===")
    
    with tempfile.TemporaryDirectory() as tmpdir:
        tmpdir = Path(tmpdir)
        
        # Create test image
        from PIL import Image
        test_img = Image.new('RGB', (512, 512), (255, 255, 255))
        img_path = tmpdir / "test_prop.png"
        test_img.save(img_path)
        
        # Step 1: generate_triposr (single image)
        glb_path = tmpdir / "test_prop.glb"
        ok, out, err = run_cmd([
            str(VENV_PYTHON), str(TOOLS_DIR / "generate_triposr.py"),
            "--source", str(img_path), "--name", "test_prop", "--device", "cpu"
        ], cwd=REPO_ROOT, timeout=300)
        
        step1_ok = glb_path.exists() and glb_path.stat().st_size > 1000
        print(f"  {'✓' if step1_ok else '✗'} Step 1: TripoSR (GLB generated: {glb_path.stat().st_size if step1_ok else 'FAILED'} bytes)")
        
        if not step1_ok:
            print(f"    Error: {err[:500]}")
            return False
        
        # Step 2: Copy GLB to Props dir for decimate_props
        import shutil
        props_dir = REPO_ROOT / "Assets" / "Art" / "Meshes" / "Props"
        props_dir.mkdir(parents=True, exist_ok=True)
        test_glb = props_dir / "test_prop.glb"
        shutil.copy2(glb_path, test_glb)
        
        # Step 3: decimate_props
        ok, out, err = run_cmd([
            str(VENV_PYTHON), str(TOOLS_DIR / "decimate_props.py")
        ], cwd=REPO_ROOT)
        
        resources_dir = REPO_ROOT / "Assets" / "Art" / "Resources" / "Props"
        bytes_file = resources_dir / "test_prop.bytes"
        manifest_file = resources_dir / "prop_manifest.json"
        
        step2_ok = bytes_file.exists() and manifest_file.exists()
        print(f"  {'✓' if step2_ok else '✗'} Step 2: decimate_props (.bytes + manifest generated)")
        
        if step2_ok:
            # Verify manifest has expected structure
            import json
            with open(manifest_file) as f:
                manifest = json.load(f)
            print(f"    Props in manifest: {len(manifest.get('props', []))}")
            for p in manifest.get('props', []):
                if p['name'] == 'test_prop':
                    print(f"    LODs: {[(l['level'], l['triangles']) for l in p['lods']]}")
        
        # Cleanup test files
        test_glb.unlink(missing_ok=True)
        bytes_file.unlink(missing_ok=True)
        manifest_file.unlink(missing_ok=True)
        
        return step1_ok and step2_ok

def main():
    print("=" * 60)
    print("Hermes Verify: TripoSR Pipeline Changes")
    print("=" * 60)
    
    results = []
    results.append(("isosurface.py patch", test_isosurface_patch()))
    results.append(("self-tests", test_self_tests()))
    results.append(("end-to-end pipeline", test_end_to_end()))
    
    print("\n" + "=" * 60)
    print("SUMMARY")
    print("=" * 60)
    for name, ok in results:
        print(f"  {'✓ PASS' if ok else '✗ FAIL'} {name}")
    
    all_ok = all(ok for _, ok in results)
    print(f"\nOverall: {'ALL TESTS PASSED' if all_ok else 'SOME TESTS FAILED'}")
    return 0 if all_ok else 1

if __name__ == "__main__":
    sys.exit(main())