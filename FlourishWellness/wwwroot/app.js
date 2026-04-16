window.downloadFile = function (fileName, base64Content) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:text/csv;base64,' + base64Content;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Unsaved-changes guard: warn when the user tries to close/reload the tab or browser
window.flourish = window.flourish || {};

flourish._leaveHandler = null;

flourish.enableLeaveWarning = function () {
    if (flourish._leaveHandler) return; // already registered
    flourish._leaveHandler = function (e) {
        e.preventDefault();
        e.returnValue = '';
    };
    window.addEventListener('beforeunload', flourish._leaveHandler);
};

flourish.disableLeaveWarning = function () {
    if (flourish._leaveHandler) {
        window.removeEventListener('beforeunload', flourish._leaveHandler);
        flourish._leaveHandler = null;
    }
};

// ─── PDF Export ─────────────────────────────────────────────────────────────

flourish.exportActionPlanPdf = async function (data, filename) {
    const { jsPDF } = window.jspdf;

    // ── Page constants ──────────────────────────────────────────────────────
    const PAGE_W = 215.9;
    const PAGE_H = 279.4;
    const ML = 18;          // left margin
    const MR = 18;          // right margin
    const CW = PAGE_W - ML - MR;   // content width (179.9 mm)
    const BOTTOM_MARGIN = 18;

    // ── Brand colours (R, G, B) ─────────────────────────────────────────────
    const C_DARK_BAR  = [20, 55, 75];      // dark navy header bars
    const C_PRIMARY   = [25, 104, 135];    // #196887 teal
    const C_DARKGREEN = [56, 124, 43];     // #387c2b
    const C_TEXT      = [95, 96, 98];      // #5f6062
    const C_LGRAY     = [209, 211, 213];   // #d1d3d5
    const C_WHITE     = [255, 255, 255];

    const doc = new jsPDF({ unit: 'mm', format: 'letter', orientation: 'portrait' });

    let y = 0;  // current Y position

    // ── Helpers ─────────────────────────────────────────────────────────────

    const setColor = (rgb) => doc.setTextColor(rgb[0], rgb[1], rgb[2]);
    const setFill  = (rgb) => doc.setFillColor(rgb[0], rgb[1], rgb[2]);
    const setDraw  = (rgb) => doc.setDrawColor(rgb[0], rgb[1], rgb[2]);

    const checkBreak = (needed) => {
        if (y + needed > PAGE_H - BOTTOM_MARGIN) {
            doc.addPage();
            y = 18;
        }
    };

    // Force a page break regardless (used between growth areas when needed).
    const forceBreak = () => {
        doc.addPage();
        y = 18;
    };

    // Estimate total height (mm) for one growth area so we can decide to
    // break before it rather than mid-way through its opening block.
    const estimateAreaOpeningHeight = (area) => {
        // header bar + focus row + smart goal (at least 1 line) + action-steps label
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(9);
        const goalLabel = `SMART Goal #1:`;  // approximate label width
        const goalIndent = ML + 4 + doc.getTextWidth(goalLabel) + 3;
        const goalMaxW = PAGE_W - MR - goalIndent;
        const goalLineCount = doc.splitTextToSize(area.smartGoal || '', goalMaxW).length || 1;
        return 6.5          // header bar
             + 7            // focus row
             + 4.5 + (goalLineCount * 4.8)  // SMART goal
             + 8;           // action steps label
    };

    // Load a URL as a base64 data-URL.  Returns null on failure.
    const fetchDataUrl = async (url) => {
        try {
            const resp = await fetch(url);
            const blob = await resp.blob();
            return await new Promise(resolve => {
                const reader = new FileReader();
                reader.onloadend = () => resolve(reader.result);
                reader.onerror   = () => resolve(null);
                reader.readAsDataURL(blob);
            });
        } catch { return null; }
    };

    // Render an SVG URL to a PNG data-URL at the given pixel dimensions.
    const svgToPng = async (url, pw, ph) => {
        try {
            const svgText = await fetch(url).then(r => r.text());
            const blob = new Blob([svgText], { type: 'image/svg+xml' });
            const blobUrl = URL.createObjectURL(blob);
            return await new Promise((resolve, reject) => {
                const img = new Image();
                img.onload = () => {
                    const cv = document.createElement('canvas');
                    cv.width = pw; cv.height = ph;
                    cv.getContext('2d').drawImage(img, 0, 0, pw, ph);
                    URL.revokeObjectURL(blobUrl);
                    resolve(cv.toDataURL('image/png'));
                };
                img.onerror = () => { URL.revokeObjectURL(blobUrl); reject(); };
                img.src = blobUrl;
            });
        } catch { return null; }
    };

    const formatDate = (s) => {
        if (!s) return '';
        const d = new Date(s);
        if (isNaN(d)) return s;
        return `${d.getMonth() + 1}/${d.getDate()}/${d.getFullYear()}`;
    };

    // ── Load images (parallel) ───────────────────────────────────────────────
    const [americareImg, flourishImg] = await Promise.all([
        fetchDataUrl('/images/Americare-SL.png'),
        svgToPng('/images/logo.svg', 300, 200)
    ]);

    // ═══════════════════════════════════════════════════════════════════════
    //  PAGE 1 – HEADER
    // ═══════════════════════════════════════════════════════════════════════
    y = 14;

    // Americare logo – top left
    if (americareImg) {
        doc.addImage(americareImg, 'PNG', ML, y, 34, 17);
    }

    // Flourish hummingbird logo – centered
    if (flourishImg) {
        const imgW = 28, imgH = 19;
        doc.addImage(flourishImg, 'PNG', (PAGE_W - imgW) / 2, y - 2, imgW, imgH);
    }

    // "Flourish" text (large, centered)
    y += 14;
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(20);
    setColor(C_PRIMARY);
    doc.text('Flourish', PAGE_W / 2, y, { align: 'center' });

    // "WELLNESS™" sub-text
    y += 7;
    doc.setFontSize(11);
    setColor(C_DARKGREEN);
    doc.text('WELLNESS\u2122', PAGE_W / 2, y, { align: 'center' });

    // "COMMUNITY AUDIT ACTION PLAN"
    y += 7;
    doc.setFontSize(10);
    setColor(C_DARKGREEN);
    doc.setFont('helvetica', 'bold');
    doc.text('COMMUNITY AUDIT ACTION PLAN', PAGE_W / 2, y, { align: 'center' });
    y += 4;

    // Horizontal rule under header
    setDraw(C_LGRAY);
    doc.setLineWidth(0.3);
    doc.line(ML, y, PAGE_W - MR, y);
    y += 4;

    // ── Community Information ───────────────────────────────────────────────
    // Header bar
    setFill(C_DARK_BAR);
    doc.rect(ML, y, CW, 6.5, 'F');
    setColor(C_WHITE);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(9);
    doc.text('Community Information', ML + 2.5, y + 4.5);
    y += 6.5;

    // Border box around info section
    const infoStartY = y;
    const infoFields = [
        ['Community Name:', data.communityName || ''],
        ['Executive Director', data.executiveDirector || ''],
        ['RDO:', data.rdo || ''],
        ['Date Audit Completed:', formatDate(data.dateAuditCompleted) || data.dateAuditCompleted || ''],
        ['Date of Audit Review Call:', formatDate(data.dateAuditReviewCall)],
        ['Date Action Plan Approved by RDO:', formatDate(data.dateApprovalByRdo)],
    ];

    const labelColW = 68;
    for (const [label, value] of infoFields) {
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8.5);
        setColor(C_TEXT);
        doc.text(label, ML + 2.5, y + 4);
        doc.setFont('helvetica', 'normal');
        doc.text(value, ML + labelColW, y + 4);
        y += 5.5;
    }

    // draw border around community info rows
    setDraw(C_LGRAY);
    doc.setLineWidth(0.25);
    doc.rect(ML, infoStartY, CW, y - infoStartY);
    y += 5;

    // ═══════════════════════════════════════════════════════════════════════
    //  GROWTH AREAS
    // ═══════════════════════════════════════════════════════════════════════
    const areas = data.growthAreas || [];
    let areaNum = 0;

    for (const area of areas) {
        areaNum++;

        // ── Page break decision ─────────────────────────────────────────────
        // If the opening block of this area won't fit, start a fresh page.
        const openingH = estimateAreaOpeningHeight(area);
        if (y + openingH > PAGE_H - BOTTOM_MARGIN) {
            forceBreak();
        }

        // ── Growth Area header bar ──────────────────────────────────────────
        setFill(C_DARK_BAR);
        doc.rect(ML, y, CW, 6.5, 'F');
        setColor(C_WHITE);
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(10);
        doc.text(`Growth Area #${areaNum}:`, ML + 2.5, y + 4.5);
        y += 6.5;

        // ── Focus ───────────────────────────────────────────────────────────
        // (kept with header — no break between them)
        doc.setFontSize(8.5);
        setColor(C_PRIMARY);
        doc.setFont('helvetica', 'bold');
        doc.text('Flourish Wellness Focus:', ML + 3, y + 4.5);
        setColor(C_TEXT);
        doc.setFont('helvetica', 'normal');
        const focusText = area.sectionName || '';
        const focusLines = doc.splitTextToSize(focusText, CW - 46 - 3);
        doc.text(focusLines[0] || '', ML + 3 + 46, y + 4.5);
        for (let fi = 1; fi < focusLines.length; fi++) {
            y += 4.5;
            doc.text(focusLines[fi], ML + 3 + 46, y + 4.5);
        }
        y += 7;

        // ── SMART Goal ──────────────────────────────────────────────────────
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(9);
        setColor(C_TEXT);
        const goalLabel = `SMART Goal #${areaNum}:`;
        doc.text(goalLabel, ML + 4, y + 4.5);

        const goalIndent = ML + 4 + doc.getTextWidth(goalLabel) + 3;
        const goalMaxW = PAGE_W - MR - goalIndent;
        const goalLines = doc.splitTextToSize(area.smartGoal || '', goalMaxW);
        doc.setFont('helvetica', 'normal');

        // First line on same row as label
        if (goalLines.length > 0) {
            doc.text(goalLines[0], goalIndent, y + 4.5);
        }
        // Overflow lines — each may force a page break
        for (let i = 1; i < goalLines.length; i++) {
            y += 4.8;
            checkBreak(5);
            doc.text(goalLines[i], goalIndent, y + 4.5);
        }
        y += 9;

        // ── Action Steps header ─────────────────────────────────────────────
        checkBreak(14);
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(9);
        setColor(C_TEXT);
        doc.text('Action Steps (include action, responsible party, and target date):', ML + 4, y + 4);
        y += 8;

        // ── Steps ───────────────────────────────────────────────────────────
        const NUM_X = ML + 8;
        const STEP_X = ML + 15;
        const STEP_W = PAGE_W - MR - STEP_X;
        let stepNum = 0;

        for (const step of (area.actionSteps || [])) {
            if (!step.action && !step.responsibleParty && !step.targetDate) continue;
            stepNum++;

            doc.setFont('helvetica', 'normal');
            doc.setFontSize(8.5);
            const actionLines = doc.splitTextToSize(step.action || '', STEP_W);
            const rpText = step.responsibleParty ? `Responsible: ${step.responsibleParty}` : '';
            const tdText = step.targetDate ? `Target Date: ${formatDate(step.targetDate)}` : '';

            // Keep the whole step block together on one page
            const blockH = (actionLines.length * 4.5) + (rpText ? 4.5 : 0) + (tdText ? 4.5 : 0) + 4;
            checkBreak(blockH);

            doc.setFont('helvetica', 'bold');
            setColor(C_TEXT);
            doc.text(String(stepNum), NUM_X, y + 4);

            doc.setFont('helvetica', 'normal');
            doc.text(actionLines, STEP_X, y + 4);
            y += actionLines.length * 4.5;

            if (rpText) {
                setColor(C_PRIMARY);
                doc.text(rpText, STEP_X, y + 4);
                y += 4.5;
            }
            if (tdText) {
                setColor(C_TEXT);
                doc.text(tdText, STEP_X, y + 4);
                y += 4.5;
            }
            y += 4;
        }

        y += 3;

        // Separator between areas (skip if this is the last area)
        if (areaNum < areas.length) {
            checkBreak(8);
            setDraw(C_LGRAY);
            doc.setLineWidth(0.3);
            doc.line(ML, y, PAGE_W - MR, y);
            y += 6;
        }
    }

    // ── Footer on each page ─────────────────────────────────────────────────
    const pageCount = doc.internal.getNumberOfPages();
    for (let p = 1; p <= pageCount; p++) {
        doc.setPage(p);
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(7.5);
        setColor(C_LGRAY);
        doc.text(
            `Page ${p} of ${pageCount}  •  Flourish Wellness Community Audit Action Plan`,
            PAGE_W / 2,
            PAGE_H - 8,
            { align: 'center' }
        );
    }

    // ── Save ────────────────────────────────────────────────────────────────
    if (!filename || filename.trim() === '') filename = 'ActionPlan';
    if (!filename.toLowerCase().endsWith('.pdf')) filename += '.pdf';
    doc.save(filename);
};
