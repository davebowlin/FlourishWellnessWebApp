# This is a quick and dirty database browser for FlourishWellness. It is not intended to be a full-featured tool, but rather a simple way to view table contents without needing to use SQL Server Management Studio or other external tools.
# This shows the tables and column/rows in a simple UI. It is read-only and does not support any editing or filtering. It is intended for quick lookups and debugging purposes only.


import tkinter as tk
from tkinter import messagebox, ttk, simpledialog

import pyodbc

# --- Configuration ---
DB_SERVER = "ASISQLDBPROD"
DB_DATABASE = "FlourishWellness"
DB_CONN_STR = (
    f"DRIVER={{ODBC Driver 17 for SQL Server}};"
    f"SERVER={DB_SERVER};DATABASE={DB_DATABASE};Trusted_Connection=yes;"
)


def get_connection():
    return pyodbc.connect(DB_CONN_STR)


def quote_ident(name: str) -> str:
    return "[" + name.replace("]", "]]") + "]"


class DatabaseBrowserApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("FlourishWellness - Database Browser")
        self.root.geometry("1300x760")

        self._table_lookup: dict[str, tuple[str, str]] = {}

        self.pending_updates = {}

        self._build_ui()
        self.load_tables()

    def _build_ui(self):
        top = ttk.Frame(self.root, padding="8 6")
        top.pack(fill=tk.X)

        ttk.Button(top, text="Refresh Tables", command=self.load_tables).pack(
            side=tk.LEFT, padx=(0, 8)
        )
        ttk.Button(top, text="Load Selected Table", command=self.load_selected_table).pack(
            side=tk.LEFT
        )
        ttk.Button(top, text="Delete Data", command=self.on_delete_data).pack(
            side=tk.LEFT, padx=(0, 8)
        )
        ttk.Button(top, text="Update Data", command=self.on_update_data).pack(
            side=tk.LEFT, padx=(0, 8)
        )

        self.status_var = tk.StringVar(value="Ready")
        ttk.Label(top, textvariable=self.status_var).pack(side=tk.RIGHT)

        main = ttk.PanedWindow(self.root, orient=tk.HORIZONTAL)
        main.pack(fill=tk.BOTH, expand=True, padx=8, pady=(0, 8))

        left_frame = ttk.LabelFrame(main, text="Tables", padding=6)
        right_frame = ttk.LabelFrame(main, text="Rows", padding=6)
        main.add(left_frame, weight=1)
        main.add(right_frame, weight=5)

        self.table_list = tk.Listbox(left_frame, exportselection=False)
        table_scroll = ttk.Scrollbar(
            left_frame, orient=tk.VERTICAL, command=self.table_list.yview
        )
        self.table_list.configure(yscrollcommand=table_scroll.set)

        table_scroll.pack(side=tk.RIGHT, fill=tk.Y)
        self.table_list.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        self.table_list.bind("<<ListboxSelect>>", lambda _e: self.load_selected_table())

        self.row_tree = ttk.Treeview(right_frame, show="headings")
        row_vsb = ttk.Scrollbar(right_frame, orient=tk.VERTICAL, command=self.row_tree.yview)
        row_hsb = ttk.Scrollbar(
            right_frame, orient=tk.HORIZONTAL, command=self.row_tree.xview
        )
        self.row_tree.configure(yscrollcommand=row_vsb.set, xscrollcommand=row_hsb.set)

        row_vsb.pack(side=tk.RIGHT, fill=tk.Y)
        row_hsb.pack(side=tk.BOTTOM, fill=tk.X)
        self.row_tree.pack(fill=tk.BOTH, expand=True)

        self.row_tree.bind("<Double-1>", self.on_edit_cell)

    def load_tables(self):
        try:
            with get_connection() as conn:
                cursor = conn.cursor()
                cursor.execute(
                    """
                    SELECT TABLE_SCHEMA, TABLE_NAME
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    ORDER BY TABLE_SCHEMA, TABLE_NAME
                    """
                )
                rows = cursor.fetchall()
        except Exception as exc:
            messagebox.showerror("Database Error", str(exc))
            return

        self.table_list.delete(0, tk.END)
        self._table_lookup.clear()

        for row in rows:
            label = f"{row.TABLE_SCHEMA}.{row.TABLE_NAME}"
            self._table_lookup[label] = (row.TABLE_SCHEMA, row.TABLE_NAME)
            self.table_list.insert(tk.END, label)

        self.clear_rows()
        self.status_var.set(f"Loaded {len(rows)} table(s)")

    def clear_rows(self):
        self.row_tree.delete(*self.row_tree.get_children())
        self.row_tree["columns"] = ()

    def load_selected_table(self):
        selected = self.table_list.curselection()
        if not selected:
            return

        table_label = self.table_list.get(selected[0])
        table_info = self._table_lookup.get(table_label)
        if table_info is None:
            messagebox.showerror("Selection Error", "Unknown table selection.")
            return

        schema_name, table_name = table_info
        sql = (
            f"SELECT * FROM {quote_ident(schema_name)}.{quote_ident(table_name)}"
        )

        try:
            with get_connection() as conn:
                cursor = conn.cursor()
                cursor.execute(sql)
                rows = cursor.fetchall()
                columns = [desc[0] for desc in (cursor.description or [])]
        except Exception as exc:
            messagebox.showerror("Database Error", str(exc))
            return

        self.clear_rows()
        self.row_tree["columns"] = columns

        for col in columns:
            self.row_tree.heading(col, text=col)
            self.row_tree.column(col, width=170, minwidth=80, anchor=tk.W)

        for row in rows:
            values = ["" if value is None else str(value) for value in row]
            self.row_tree.insert("", tk.END, values=values)

        self.status_var.set(
            f"{table_label}: {len(rows)} row(s), {len(columns)} column(s)"
        )

    def on_edit_cell(self, event):
        """Enable editing of a cell in the TreeView."""
        item_id = self.row_tree.identify_row(event.y)
        column_id = self.row_tree.identify_column(event.x)

        if not item_id or not column_id:
            return

        column_index = int(column_id.replace("#", "")) - 1
        column_name = self.row_tree["columns"][column_index]
        old_value = self.row_tree.item(item_id, "values")[column_index]

        new_value = simpledialog.askstring("Edit Cell", f"Enter new value for {column_name}:", initialvalue=old_value)
        if new_value is not None:
            values = list(self.row_tree.item(item_id, "values"))
            values[column_index] = new_value
            self.row_tree.item(item_id, values=values)

            # Store the changes for later update
            self.pending_updates[item_id] = self.pending_updates.get(item_id, {})
            self.pending_updates[item_id][column_name] = new_value

    def on_delete_data(self):
        """Prompt user to delete data from the selected table."""
        selected = self.table_list.curselection()
        if not selected:
            messagebox.showwarning("Warning", "Please select a table first.")
            return

        table_label = self.table_list.get(selected[0])
        table_info = self._table_lookup.get(table_label)
        if table_info is None:
            messagebox.showerror("Selection Error", "Unknown table selection.")
            return

        schema_name, table_name = table_info
        condition = simpledialog.askstring("Delete Data", f"Enter the condition for deletion in {table_name} (e.g., UserId = 123):")
        if condition:
            try:
                with get_connection() as conn:
                    cursor = conn.cursor()
                    sql = f"DELETE FROM {quote_ident(schema_name)}.{quote_ident(table_name)} WHERE {condition}"
                    cursor.execute(sql)
                    conn.commit()
                    messagebox.showinfo("Success", f"Data deleted from {table_name} where {condition}")
            except Exception as exc:
                messagebox.showerror("Database Error", str(exc))


    def on_update_data(self):
        """Write pending changes to the database."""
        selected = self.table_list.curselection()
        if not selected:
            messagebox.showwarning("Warning", "Please select a table first.")
            return

        table_label = self.table_list.get(selected[0])
        table_info = self._table_lookup.get(table_label)
        if table_info is None:
            messagebox.showerror("Selection Error", "Unknown table selection.")
            return

        schema_name, table_name = table_info
        try:
            with get_connection() as conn:
                cursor = conn.cursor()
                for item_id, updates in self.pending_updates.items():
                    set_clause = ", ".join([f"{col} = ?" for col in updates.keys()])
                    sql = f"UPDATE {quote_ident(schema_name)}.{quote_ident(table_name)} SET {set_clause} WHERE Id = ?"
                    params = list(updates.values()) + [self.row_tree.item(item_id, "values")[0]]
                    cursor.execute(sql, params)
                conn.commit()

            self.pending_updates.clear()
            messagebox.showinfo("Success", "All changes have been saved.")
        except Exception as exc:
            messagebox.showerror("Database Error", str(exc))


def fetch_tables():
    """Fetch the list of tables from the database."""
    try:
        with pyodbc.connect(DB_CONN_STR) as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            return [row[0] for row in cursor.fetchall()]
    except Exception as e:
        messagebox.showerror("Error", f"Failed to fetch tables: {e}")
        return []


def fetch_table_data(table_name):
    """Fetch data from the selected table."""
    try:
        with pyodbc.connect(DB_CONN_STR) as conn:
            cursor = conn.cursor()
            cursor.execute(f"SELECT * FROM {table_name}")
            columns = [column[0] for column in cursor.description]
            rows = cursor.fetchall()
            return columns, rows
    except Exception as e:
        messagebox.showerror("Error", f"Failed to fetch data: {e}")
        return [], []


def delete_data_from_table(table_name, condition):
    """Delete data from the specified table based on a condition."""
    try:
        with pyodbc.connect(DB_CONN_STR) as conn:
            cursor = conn.cursor()
            cursor.execute(f"DELETE FROM {table_name} WHERE {condition}")
            conn.commit()
            messagebox.showinfo("Success", f"Data deleted from {table_name} where {condition}")
    except Exception as e:
        messagebox.showerror("Error", f"Failed to delete data: {e}")


def update_data_in_table(table_name, column_values, condition):
    """Update data in the specified table based on a condition."""
    try:
        with pyodbc.connect(DB_CONN_STR) as conn:
            cursor = conn.cursor()
            set_clause = ", ".join([f"{col} = ?" for col in column_values.keys()])
            sql = f"UPDATE {table_name} SET {set_clause} WHERE {condition}"
            cursor.execute(sql, list(column_values.values()))
            conn.commit()
            messagebox.showinfo("Success", f"Data updated in {table_name} where {condition}")
    except Exception as e:
        messagebox.showerror("Error", f"Failed to update data: {e}")


if __name__ == "__main__":
    root = tk.Tk()
    app = DatabaseBrowserApp(root)
    root.mainloop()
