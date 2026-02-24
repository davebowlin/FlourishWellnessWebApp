-- Migration: Add ParentSectionId to Sections for subsection support
ALTER TABLE Sections ADD COLUMN ParentSectionId INTEGER REFERENCES Sections(Id);
