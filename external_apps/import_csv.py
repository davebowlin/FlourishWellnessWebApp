# This is used to import the sections, subsections, and questions into the db.
import tkinter as tk
from tkinter import filedialog, messagebox
import sqlite3
import csv
import os

def import_csv_to_db(csv_path, db_path):
    """
    Import sections, subsections, and questions from CSV to SQLite database.
    
    CSV Format:
    Column A: Section Name (parent section)
    Column B: Subsection Name (leave empty if no subsection)
    Column C: Question Text
    
    Examples:
    "Mental Health","","Does your org have a mental health policy?"
    "Mental Health","Employee Support","Are EAP services available?"
    "Physical Wellness","","Is there a gym on site?"
    """
    conn = sqlite3.connect(db_path)
    cur = conn.cursor()

    section_cache = {}  # Cache section IDs to avoid duplicate lookups
    stats = {"sections": 0, "subsections": 0, "questions": 0, "skipped": 0, "duplicate_questions": 0}

    import itertools
    with open(csv_path, newline='', encoding='utf-8-sig') as csvfile:
        reader = csv.reader(csvfile)
        first_row = next(reader, None)
        # Always skip the first row if it matches the known headers (case-insensitive, strip whitespace)
        header_values = ["section", "subsection", "question"]
        is_header = False
        if first_row:
            normalized = [col.strip().lower() for col in first_row]
            if normalized == header_values:
                is_header = True
        if is_header:
            rows = reader  # skip header, process rest
        else:
            rows = itertools.chain([first_row], reader)  # process first row, then rest
        for row in rows:
            process_row(cur, row, section_cache, stats)

    conn.commit()
    conn.close()
    
    return stats

def process_row(cur, row, section_cache, stats):
    """Process a single CSV row and insert section/subsection/question."""
    if len(row) < 1:
        stats["skipped"] += 1
        return  # Skip completely empty rows

    # Skip any row that matches the header (case-insensitive, ignores whitespace)
    header_values = ["section", "subsection", "question"]
    normalized = [col.strip().lower() for col in row]
    if normalized == header_values:
        stats["skipped"] += 1
        return

    section_name = row[0].strip()
    subsection_name = row[1].strip() if len(row) > 1 else ""
    question_text = row[2].strip() if len(row) > 2 else ""

    if not section_name:
        stats["skipped"] += 1
        return  # Skip rows without section name
    
    # Get or create parent section
    if section_name not in section_cache:
        cur.execute("SELECT Id FROM Sections WHERE Name = ? AND ParentSectionId IS NULL", (section_name,))
        section = cur.fetchone()
        if section:
            section_id = section[0]
        else:
            cur.execute("INSERT INTO Sections (Name, ParentSectionId) VALUES (?, NULL)", (section_name,))
            section_id = cur.lastrowid
            stats["sections"] += 1
        section_cache[section_name] = section_id
    else:
        section_id = section_cache[section_name]
    
    # Handle subsection if present
    target_section_id = section_id
    if subsection_name:
        subsection_key = f"{section_name}::{subsection_name}"
        if subsection_key not in section_cache:
            cur.execute("SELECT Id FROM Sections WHERE Name = ? AND ParentSectionId = ?", 
                       (subsection_name, section_id))
            subsection = cur.fetchone()
            if subsection:
                target_section_id = subsection[0]
            else:
                cur.execute("INSERT INTO Sections (Name, ParentSectionId) VALUES (?, ?)", 
                           (subsection_name, section_id))
                target_section_id = cur.lastrowid
                stats["subsections"] += 1
            section_cache[subsection_key] = target_section_id
        else:
            target_section_id = section_cache[subsection_key]
    
    # Insert question if provided (check for duplicates)
    if question_text:
        try:
            # Check if question already exists for this section
            cur.execute("SELECT Id FROM Questions WHERE Text = ? AND SectionId = ?", 
                       (question_text, target_section_id))
            existing = cur.fetchone()
            
            if existing:
                stats["duplicate_questions"] += 1
            else:
                cur.execute("INSERT INTO Questions (Text, SectionId) VALUES (?, ?)", 
                           (question_text, target_section_id))
                stats["questions"] += 1
        except Exception as e:
            print(f"Error inserting question '{question_text}': {e}")
            stats["skipped"] += 1
    else:
        stats["skipped"] += 1

def select_and_import():
    csv_path = filedialog.askopenfilename(
        title="Select CSV File", 
        filetypes=[("CSV Files", "*.csv"), ("All Files", "*.*")]
    )
    if not csv_path:
        return

    db_path = db_path_var.get()
    if not db_path or not os.path.exists(db_path):
        messagebox.showerror("Error", f"Database not found at {db_path}")
        return

    try:
        stats = import_csv_to_db(csv_path, db_path)
        message = f"""Import completed successfully!

Sections created: {stats['sections']}
Subsections created: {stats['subsections']}
Questions created: {stats['questions']}
Duplicate questions skipped: {stats['duplicate_questions']}
Rows skipped: {stats['skipped']}"""
        messagebox.showinfo("Success", message)
    except Exception as e:
        messagebox.showerror("Error", f"Import failed:\n{e}")

# Create GUI
root = tk.Tk()
root.title("FlourishWellness CSV Importer")
root.geometry("500x300")

title_label = tk.Label(
    root, 
    text="Import Sections, Subsections & Questions", 
    font=("Arial", 14, "bold")
)
title_label.pack(pady=15)

info_label = tk.Label(
    root, 
    text="CSV Format:\nColumn A: Section | Column B: Subsection (optional) | Column C: Question",
    font=("Arial", 10),
    justify="left"
)
info_label.pack(pady=5)

# Database selection
db_frame = tk.Frame(root)
db_frame.pack(pady=10)

db_path_var = tk.StringVar()
db_path_var.set("")

def select_db_file():
    path = filedialog.askopenfilename(
        title="Select Database File",
        filetypes=[("SQLite DB", "*.db"), ("All Files", "*.*")]
    )
    if path:
        db_path_var.set(path)

db_label = tk.Label(db_frame, text="Database File:", font=("Arial", 10))
db_label.pack(side=tk.LEFT, padx=(0,5))
db_entry = tk.Entry(db_frame, textvariable=db_path_var, width=40)
db_entry.pack(side=tk.LEFT, padx=(0,5))
db_btn = tk.Button(db_frame, text="Browse", command=select_db_file)
db_btn.pack(side=tk.LEFT)

import_btn = tk.Button(
    root, 
    text="Select CSV and Import", 
    command=select_and_import, 
    font=("Arial", 12), 
    width=25,
    bg="#007bff",
    fg="white",
    relief="raised",
    cursor="hand2"
)
import_btn.pack(pady=20)

note_label = tk.Label(
    root,
    text="Note: Select the database file to import into.",
    font=("Arial", 9),
    fg="gray"
)
note_label.pack(pady=5)

root.mainloop()
