
import os

with open("missing_images_report.txt", "r") as f:
    lines = f.readlines()

created_dirs = set()

for line in lines:
    if "Missing " in line:
        path = line.split("Missing ")[1].strip()
        # path is like /uploads/products/gpu/asus-tuf-rtx-5090-32g/thumb.jpg
        dir_path = os.path.dirname(os.path.join("wwwroot", path.lstrip("/")))
        if dir_path not in created_dirs:
            os.makedirs(dir_path, exist_ok=True)
            created_dirs.add(dir_path)
            print(f"Created directory: {dir_path}")

print(f"\nTotal directories created: {len(created_dirs)}")
